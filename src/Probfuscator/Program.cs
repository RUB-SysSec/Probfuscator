using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Microsoft.Cci;
using Microsoft.Cci.MutableCodeModel;
using Cfg;
using Log;
using CfgElements;
using Graph;
using GraphElements;
using Transformations;

namespace Probfuscator {
    class Program {

        // Taken from http://stackoverflow.com/questions/1344221/how-can-i-generate-random-alphanumeric-strings-in-c?page=1&tab=votes#tab-top
        private static Random PRNGRandomInterfaces;
        public static string randomString(int length) {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[PRNGRandomInterfaces.Next(s.Length)]).ToArray());
        }


        static void Main(string[] args) {
            String inputFile;
            String targetFile;
            String targetNamespace;
            String targetClass;
            String targetMethod;
            int depth;
            int dimension;
            int numberValidPaths;
            int duplicateBasicBlockWeight;
            int duplicateBasicBlockCorrectionValue;
            int stateChangeWeight;
            int stateChangeCorrectionValue;
            int insertOpaquePredicateWeight;
            int seed;

            // Add debugging code into the obfuscated method (dump obfuscation graphs and so on)
            bool graphTransformerDebug = false;

            // Should the obfuscated code contain information to trace the control flow?
            bool basicBlockTrace = false;

            // When debugging is active, should the whole obfuscation graph be dumped or only the vpaths in it?
            bool graphOnlyDumpVPaths = true;

            // The number of random interfaces that are added to the program
            int numberRandomInterfaces = 100;

            if (args.Length != 14) {
                System.Console.WriteLine("Needed parameters: <inputBinary> <outputBinary> <namespace> <class> <method> <depth> <dimension> <numberValidPaths> <duplicateBasicBlockWeight> <duplicateBasicBlockCorrectionValue> <stateChangeWeight> <stateChangeCorrectionValue> <insertOpaquePredicateWeight> <seed>");
                return;
            }
            else {
                inputFile = args[0];
                targetFile = args[1];
                targetNamespace = args[2];
                targetClass = args[3];
                targetMethod = args[4];
                depth = Convert.ToInt32(args[5]);
                dimension = Convert.ToInt32(args[6]);
                numberValidPaths = Convert.ToInt32(args[7]);
                duplicateBasicBlockWeight = Convert.ToInt32(args[8]);
                duplicateBasicBlockCorrectionValue = Convert.ToInt32(args[9]);
                stateChangeWeight = Convert.ToInt32(args[10]);
                stateChangeCorrectionValue = Convert.ToInt32(args[11]);
                insertOpaquePredicateWeight = Convert.ToInt32(args[12]);
                seed = Convert.ToInt32(args[13]);
            }

            String logDir = Path.GetDirectoryName(targetFile);
            Log.Log logger = new Log.Log(logDir, "probfuscation_logfile.txt");

            System.Console.WriteLine("Obfuscating: " + inputFile);
            logger.writeLine("Obfuscating: " + inputFile);
            System.Console.WriteLine("Output file: " + targetFile);
            logger.writeLine("Output file: " + targetFile);
            System.Console.WriteLine("Target namespace: " + targetNamespace);
            logger.writeLine("Target namespace: " + targetNamespace);
            System.Console.WriteLine("Target class: " + targetClass);
            logger.writeLine("Target class: " + targetClass);
            System.Console.WriteLine("Target method: " + targetMethod);
            logger.writeLine("Target method: " + targetMethod);
            System.Console.WriteLine("Depth: " + depth);
            logger.writeLine("Depth: " + depth);
            System.Console.WriteLine("Dimension: " + dimension);
            logger.writeLine("Dimension: " + dimension);
            System.Console.WriteLine("Number of vpaths: " + numberValidPaths);
            logger.writeLine("Number of vpaths: " + numberValidPaths);
            System.Console.WriteLine("Basic Block duplication weight: " + duplicateBasicBlockWeight);
            logger.writeLine("Basic Block duplication weight: " + duplicateBasicBlockWeight);
            System.Console.WriteLine("Basic Block duplication correction value: " + duplicateBasicBlockCorrectionValue);
            logger.writeLine("Basic Block duplication correction value: " + duplicateBasicBlockCorrectionValue);
            System.Console.WriteLine("State change weight: " + stateChangeWeight);
            logger.writeLine("State change weight: " + stateChangeWeight);
            System.Console.WriteLine("State change correction value: " + stateChangeCorrectionValue);
            logger.writeLine("State change correction value: " + stateChangeCorrectionValue);
            System.Console.WriteLine("Opaque predicate weight: " + insertOpaquePredicateWeight);
            logger.writeLine("Opaque predicate weight: " + insertOpaquePredicateWeight);
            System.Console.WriteLine("Seed: " + seed);
            logger.writeLine("Seed: " + seed);

            // Seed PRNG for interfaces
            PRNGRandomInterfaces = new Random(seed);

            using (var host = new PeReader.DefaultHost()) {
                IModule/*?*/ module = host.LoadUnitFrom(inputFile) as IModule;

                if (module == null || module == Dummy.Module || module == Dummy.Assembly) {
                    Console.WriteLine(inputFile + " is not a PE file containing a CLR module or assembly.");
                    return;
                }

                module = new MetadataDeepCopier(host).Copy(module);

                if (module as Assembly == null) {
                    logger.writeLine("File does not have CIL assembly");
                    return;
                }

                // create analyzer object object                
                CfgBuilder analyze = new Cfg.CfgBuilder(module, host, logger);

                PdbReader/*?*/ pdbReader = null;
                string pdbFile = Path.ChangeExtension(module.Location, "pdb");
                if (File.Exists(pdbFile)) {
                    using (var pdbStream = File.OpenRead(pdbFile)) {
                        pdbReader = new PdbReader(pdbStream, host);
                    }
                }
                else {
                    logger.writeLine("Could not load the PDB file for '" + module.Name.Value + "' . Proceeding anyway.");
                }

                using (pdbReader) {

                    Microsoft.Cci.ILGenerator.LocalScopeProvider localScopeProvider = null;
                    if (pdbReader != null) {
                        localScopeProvider = new ILGenerator.LocalScopeProvider(pdbReader);
                    }

                    // search the namespace the interface should be added to
                    IUnitNamespace foundNamespace = null;
                    foreach (var tempMember in module.UnitNamespaceRoot.Members) {

                        if ((tempMember as IUnitNamespace) == null) {
                            continue;
                        }

                        IUnitNamespace tempNamespace = (tempMember as IUnitNamespace);

                        if (tempNamespace.ToString() == targetNamespace) {
                            foundNamespace = tempNamespace;
                            break;
                        }
                    }
                    if (foundNamespace == null) {
                        throw new ArgumentException("Not able to find target namespace.");
                    }

                    // add created interface (and implemented methods) to all classes
                    bool classFound = false;
                    foreach (var tempClass in module.GetAllTypes()) {
                        if ((tempClass as NamespaceTypeDefinition) == null
                            || tempClass.IsAbstract) {
                            continue;
                        }

                        NamespaceTypeDefinition foundClass = (tempClass as NamespaceTypeDefinition);

                        if (foundClass.ContainingUnitNamespace.ToString() == "") {
                            continue;
                        }
                        if (foundClass.ToString() != targetNamespace + "." + targetClass) {
                            continue;
                        }
                        classFound = true;

                        Random prng = new Random();
                        GraphTransformer graphTransformer = new GraphTransformer(module, host, logger, prng, foundNamespace, foundClass, depth, dimension, graphTransformerDebug);

                        graphTransformer.duplicateBasicBlockWeight = duplicateBasicBlockWeight;
                        graphTransformer.duplicateBasicBlockCorrectionValue = duplicateBasicBlockCorrectionValue;
                        graphTransformer.stateChangeWeight = stateChangeWeight;
                        graphTransformer.stateChangeCorrectionValue = stateChangeCorrectionValue;
                        graphTransformer.insertOpaquePredicateWeight = insertOpaquePredicateWeight;
                        graphTransformer.trace = basicBlockTrace;
                        graphTransformer.graphOnlyDumpVPaths = graphOnlyDumpVPaths;
                        graphTransformer.debuggingDumpLocation = logDir;

                        // Add 100 random interfaces to the namespace
                        Helper testHelper = new Helper(module, host, logger);
                        List<NamespaceTypeDefinition> randomInterfaces = new List<NamespaceTypeDefinition>();
                        for (int i = 0; i < numberRandomInterfaces; i++) {
                            String randName = randomString(20);
                            NamespaceTypeDefinition temp = testHelper.createNewInterface(randName, foundNamespace);
                            randomInterfaces.Add(temp);
                        }

                        InterfaceTransformer interfaceTransformer = new InterfaceTransformer(module, host, logger);
                        foreach (var classToAdd in module.GetAllTypes()) {
                            if ((classToAdd as NamespaceTypeDefinition) == null
                                || classToAdd.IsAbstract
                                || classToAdd.IsInterface
                                || classToAdd.IsEnum
                                || classToAdd.IsDelegate
                                || classToAdd.IsGeneric
                                || classToAdd.IsStruct) {
                                continue;
                            }

                            if (((NamespaceTypeDefinition)classToAdd).ContainingUnitNamespace.ToString() == "") {
                                continue;
                            }

                            /*
                            // Use this code if you want to add standard interfaces to the target class
                            interfaceTransformer.addStdInterfacesGivenByFile(@"e:\code\dotnet_standard_interfaces.txt");

                            // add std interfaces to class
                            if (foundClass != (classToAdd as NamespaceTypeDefinition)) {
                                foreach (ITypeDefinition temp in interfaceTransformer.getInterfacesList()) {
                                    interfaceTransformer.addInterface((classToAdd as NamespaceTypeDefinition), temp);
                                }
                            }
                            */

                            // Add random interfaces to the classes
                            List<NamespaceTypeDefinition> alreadyAdded = new List<NamespaceTypeDefinition>();
                            int max = PRNGRandomInterfaces.Next(numberRandomInterfaces);
                            NamespaceTypeDefinition interfaceClass = (classToAdd as NamespaceTypeDefinition);
                            logger.writeLine("Adding " + max + " random interfaces to class \"" + interfaceClass.ToString() + "\"");
                            for (int i = 0; i < max; i++) {
                                NamespaceTypeDefinition randInterface = randomInterfaces.ElementAt(PRNGRandomInterfaces.Next(randomInterfaces.Count));
                                if (alreadyAdded.Contains(randInterface)) {
                                    continue;
                                }
                                alreadyAdded.Add(randInterface);
                                logger.writeLine("Adding interface: \"" + randInterface.ToString() + "\"");

                                // add nodes interface to class
                                if (interfaceClass.Interfaces != null) {
                                    interfaceClass.Interfaces.Add(randInterface);
                                }
                                else {
                                    interfaceClass.Interfaces = new List<ITypeReference>();
                                    interfaceClass.Interfaces.Add(randInterface);
                                }
                            }
                            logger.writeLine("");

                            // Add special interface for the obfuscation scheme to the class
                            // (makes sure that all needed attributes and methods are implemented)
                            graphTransformer.addNodeInterfaceToTargetClass((classToAdd as NamespaceTypeDefinition));
                        }

                        // Prepare obfuscation graph
                        graphTransformer.generateGraph(numberValidPaths);
                        graphTransformer.createGraphMethods();

                        // Search method to obfuscate
                        MethodDefinition methodToObfu = null;
                        foreach (MethodDefinition tempMethod in foundClass.Methods) {
                            if (tempMethod.Name.ToString() == targetMethod) {
                                methodToObfu = tempMethod;
                                break;
                            }
                        }
                        if (methodToObfu == null) {
                            throw new ArgumentException("Not able to find target method.");
                        }

                        // Obfuscate target method
                        MethodCfg cfg = analyze.buildCfgForMethod(methodToObfu);
                        logger.dumpMethodCfg(cfg, "before");
                        graphTransformer.addObfuscationToMethod(cfg);
                        analyze.createMethodFromCfg(cfg);
                        logger.dumpMethodCfg(cfg, "after");

                        break;
                    }
                    if (!classFound) {
                        throw new ArgumentException("Not able to find target class.");
                    }


                    /*
                     * This code can be used if not only one specific method should be obfuscated,
                     * but the whole class.
                    List<ClassCfg> classCfgList = new List<ClassCfg>();
                    foreach (var tempClass in module.GetAllTypes()) {
                        if ((tempClass as NamespaceTypeDefinition) == null
                            || tempClass.IsAbstract) {
                            continue;
                        }

                        // create basic blocks
                        NamespaceTypeDefinition foundClass = (tempClass as NamespaceTypeDefinition);

                        logger.writeLine("Create CFG for class \"" + foundClass.Name.ToString() + "\"");
                        ClassCfg temp = analyze.buildCfgForClass(foundClass);
                        classCfgList.Add(temp);
                        logger.writeLine("\n---------------------------------\n");
                    }

                    // transform each function
                    NopTransformer transformator = new NopTransformer(module, host, logger);
                    foreach (ClassCfg tempClassCfg in classCfgList) {
                        foreach (MethodCfg tempMethodCfg in tempClassCfg.methodCfgs) {
                            logger.writeLine("Transform method CFG of \"" + tempMethodCfg.method.ToString() + "\"");
                            transformator.addNopsToCfg(tempMethodCfg);
                            logger.writeLine("\n---------------------------------\n");
                        }
                    }

                    foreach (ClassCfg tempClassCfg in classCfgList) {
                        logger.writeLine("Create class from CFG for \"" + tempClassCfg.classObj.Name.ToString() + "\"");
                        analyze.createClassFromCfg(tempClassCfg);
                        logger.writeLine("\n---------------------------------\n");
                    }
                    */

                    using (var peStream = File.Create(targetFile)) {
                        using (var pdbWriter = new PdbWriter(Path.ChangeExtension(targetFile, ".pdb"), pdbReader)) {
                            PeWriter.WritePeToStream(module, host, peStream, pdbReader, localScopeProvider, pdbWriter);
                        }
                    }
                }
            }
        }
    }
}
