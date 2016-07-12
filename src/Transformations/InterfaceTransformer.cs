using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Microsoft.Cci;
using Microsoft.Cci.MutableCodeModel;
using GraphElements;
using Log;

namespace Transformations {
    public class InterfaceTransformer {

        Log.Log logger = null;
        PeReader.DefaultHost host = null;
        IModule module = null;
        Helper helperClass = null;

        // list of all interfaces that are known to this transformation class
        private List<ITypeDefinition> interfacesList = new List<ITypeDefinition>();


        public InterfaceTransformer(IModule module, PeReader.DefaultHost host, Log.Log logger) {

            this.host = host;
            this.logger = logger;
            this.module = module;
            this.helperClass = new Helper(module, host, logger);

        }


        // gets recursivly a list of all interfaces that are used by this interface
        private void getListOfAllImplementedInterfaces(ITypeDefinition classInterface) {

            // check if the interface has other interfaces that have to be implemented
            // if not => just add this interface
            if (classInterface.Interfaces == null) {
                if (this.interfacesList.Contains(classInterface)) {
                    this.logger.writeLine("Interface \"" + classInterface.ToString() + "\" already in list");
                    return;
                }
                this.logger.writeLine("Add interface to list: " + classInterface.ToString());
                this.interfacesList.Add(classInterface);
            }
            // else => add all interfaces that should be implemented to the list
            else {
                if (!this.interfacesList.Contains(classInterface)) {
                    this.logger.writeLine("Add interface to list: " + classInterface.ToString());
                    this.interfacesList.Add(classInterface);
                }
                else {
                    this.logger.writeLine("Interface \"" + classInterface.ToString() + "\" already in list");
                }

                for (int i = 0; i < classInterface.Interfaces.Count(); i++) {

                    // only add interfaces if it implements ITypeDefinition
                    if (classInterface.Interfaces.ElementAt(i) as ITypeDefinition != null) {
                        ITypeDefinition implementedInterface = (ITypeDefinition)classInterface.Interfaces.ElementAt(i);
                        if (this.interfacesList.Contains(implementedInterface)) {
                            this.logger.writeLine("Interface \"" + implementedInterface.ToString() + "\" already in list");
                            continue;
                        }
                        this.logger.writeLine("Add interface to list: " + implementedInterface.ToString());
                        this.interfacesList.Add(implementedInterface);

                        // recursivly add all interfaces that extends this one to the interface list
                        this.logger.writeLine("Resolve inheritance of interface: " + implementedInterface.ToString());
                        getListOfAllImplementedInterfaces(implementedInterface);

                        continue;
                    }

                    // check if the ResolvedType implements the ITypeDefinition
                    else if (classInterface.Interfaces.ElementAt(i).ResolvedType as ITypeDefinition != null) {

                        ITypeDefinition implementedInterface = (ITypeDefinition)classInterface.Interfaces.ElementAt(i).ResolvedType;
                        if (this.interfacesList.Contains(implementedInterface)) {
                            this.logger.writeLine("Interface \"" + implementedInterface.ToString() + "\" already in list");
                            continue;
                        }
                        this.logger.writeLine("Add interface to list: " + implementedInterface.ToString());
                        this.interfacesList.Add(implementedInterface);

                        // recursivly add all interfaces that extends this one to the interface list
                        this.logger.writeLine("Resolve inheritance of interface: " + implementedInterface.ToString());
                        getListOfAllImplementedInterfaces(implementedInterface);

                        continue;
                    }
                    else {
                        throw new ArgumentException("Do not know how to handle interface.");
                    }
                }
            }
        }


        // returns the interfaces list of the transformation class
        public List<ITypeDefinition> getInterfacesList() {
            return interfacesList;
        }


        // searches for the interface given by name and adds it to the interface transformation list
        public void addInterfaceToListByName(String interfaceName) {
           
            this.logger.writeLine("Search for interface: " + interfaceName);

            var foundInterfaceNamespace = UnitHelper.FindType(this.host.NameTable, this.host.LoadAssembly(host.CoreAssemblySymbolicIdentity), interfaceName);

            // continue if interface could not be located
            if (foundInterfaceNamespace is Microsoft.Cci.Dummy) {
                this.logger.writeLine("Interface \"" + interfaceName + "\" was not found");
                this.logger.writeLine("");
                return;
            }

            // adds the interface and all "parent interfaces" to the allInterfaces list
            ITypeDefinition foundInterface = (foundInterfaceNamespace as ITypeDefinition);
            if (foundInterface == null) {
                throw new ArgumentException("Do not know how to handle interface."); 
            }
            this.logger.writeLine("Resolve inheritance of interface: " + foundInterface.ToString());
            getListOfAllImplementedInterfaces(foundInterface);

            this.logger.writeLine("");

        }


        // reads a file given by name and adds all interfaces that are given there (one interfaces name is given by line)
        public void addStdInterfacesGivenByFile(String stdInterfaceListFile) {

            String interfaceName;
            System.IO.StreamReader stdInterfacesFile = new System.IO.StreamReader(stdInterfaceListFile);
            while ((interfaceName = stdInterfacesFile.ReadLine()) != null) {
                this.addInterfaceToListByName(interfaceName);
            }
            stdInterfacesFile.Close();

        }


        // adds the given interface to the given class
        public void addInterface(NamespaceTypeDefinition addTargetClass, ITypeDefinition interfaceToAdd) {
       

            // add interface to target class
            if (addTargetClass.Interfaces != null) {

                // check if interface is already implemented by class
                if (addTargetClass.Interfaces.Contains(interfaceToAdd)) {
                    this.logger.writeLine("Class \"" + addTargetClass.ToString() + "\" already implements interface \"" + interfaceToAdd.ToString() + "\"");
                    return;
                }
                else {
                    this.logger.writeLine("Add interface \"" + interfaceToAdd.ToString() + "\" to class \"" + addTargetClass.ToString() + "\"");
                    addTargetClass.Interfaces.Add(interfaceToAdd);
                }
            }      
            else {
                List<ITypeReference> interfaceList = new List<ITypeReference>();
                interfaceList.Add(interfaceToAdd);
                addTargetClass.Interfaces = interfaceList;
            }

            // copy all attributes from the interface to the target class
            if (interfaceToAdd.Fields != null) {
                foreach (FieldDefinition field in interfaceToAdd.Fields) {
                    FieldDefinition copy = new FieldDefinition();
                    this.helperClass.copyField(copy, field);
                    copy.ContainingTypeDefinition = addTargetClass;

                    // set intern factory of the copied field to the one of the class
                    // (without it, the generated binary file will have strange results like use only the same field)
                    copy.InternFactory = addTargetClass.InternFactory;

                    addTargetClass.Fields.Add(copy);
                }
            }

            // copy all methods from the interface to the target class
            if (interfaceToAdd.Methods != null) {
                foreach (IMethodDefinition method in interfaceToAdd.Methods) {

                    // search through all methods in the target class
                    // to see if this method was already added
                    bool foundMethod = false;
                    foreach (IMethodDefinition tempMethod in addTargetClass.Methods) {

                        // skip constructors
                        if (tempMethod.IsConstructor) {
                            continue;
                        }

                        // check if the number of parameters are the same
                        if (tempMethod.ParameterCount == method.ParameterCount) {

                            // check if the parameters have the same type in the same order
                            bool parameterCorrect = true;
                            for (int i = 0; i < method.ParameterCount; i++) {
                                if (method.Parameters.ElementAt(i).Type != tempMethod.Parameters.ElementAt(i).Type) {
                                    parameterCorrect = false;
                                    break;
                                }
                            }

                            // check if the return type is the same
                            bool returnTypeCorrect = false;
                            if (method.Type.Equals(tempMethod.Type)) {
                                returnTypeCorrect = true;
                            }

                            // check if both methods are static
                            // (c# compiler does not allow static + non-static function with
                            // same signature in same class, but CIL does)
                            bool bothStatic = false;
                            if (method.IsStatic == tempMethod.IsStatic) {
                                bothStatic = true;
                            }

                            // if the parameters and return type are correct => check the name
                            if (parameterCorrect && returnTypeCorrect && bothStatic) {
                                if (method.Name.Equals(tempMethod.Name)) {

                                    // if name is the same => method already added
                                    foundMethod = true;
                                    break;
                                }
                            }
                        }
                    }
                    // skip if method was already added
                    if (foundMethod) {
                        this.logger.writeLine("Method \"" + method.Name.ToString() + "\" already exists");
                        continue;
                    }

                    this.logger.writeLine("Add method: " + method.Name.ToString());

                    // copy method
                    MethodDefinition copy = new MethodDefinition();
                    this.helperClass.copyMethod(copy, method);
                    copy.ContainingTypeDefinition = addTargetClass;


                    // generate random dead code for newly created method
                    ILGenerator ilGenerator = new ILGenerator(host, method);
                    foreach (IOperation tempOperation in CodeGenerator.generateDeadCode(true)) {
                        ilGenerator.Emit(tempOperation.OperationCode, tempOperation.Value);
                    }
                    IMethodBody newBody = new ILGeneratorMethodBody(ilGenerator, true, 8, method, Enumerable<ILocalDefinition>.Empty, Enumerable<ITypeDefinition>.Empty);
                    copy.Body = newBody;



                    // set intern factory of the copied method to the one of the class
                    // (without it, the generated binary file will have strange results like call always the same method)
                    copy.InternFactory = addTargetClass.InternFactory;

                    // set method to not abstract
                    copy.IsAbstract = false;

                    // set method to not external
                    copy.IsExternal = false;

                    // set intern factory of the copied method to the one of the class
                    // (without it, the generating of the binary file will have strange results)
                    copy.InternFactory = addTargetClass.InternFactory;

                    // add method to the class
                    addTargetClass.Methods.Add(copy);
                }
            }
        }
    }
}
