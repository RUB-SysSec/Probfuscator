using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Microsoft.Cci;
using Microsoft.Cci.MutableCodeModel;
using Log;
using Graph;
using GraphElements;
using Cfg;
using CfgElements;
using TransformationsMetadata;


namespace Transformations {


    class Target {
        public UnconditionalBranchTarget exitBranch = null;
        public BasicBlock basicBlock = null;
        public int currentValidPathId = -1;
    }


    class GraphRandomStateGenerator {

        CfgManipulator cfgManipulator = null;
        MethodCfg methodCfg = null;
        Log.Log logger = null;
        PeReader.DefaultHost host = null;
        IModule module = null;
        Helper helperClass = null;
        Graph.Graph graph = null;

        LocalDefinition tempRandomLocal = null;

        bool debugging = false;


        public GraphRandomStateGenerator(IModule module, PeReader.DefaultHost host, Log.Log logger, CfgManipulator cfgManipulator, Helper helperClass, Graph.Graph graph, MethodCfg methodCfg, bool debugging) {

            this.module = module;
            this.host = host;
            this.logger = logger;
            this.cfgManipulator = cfgManipulator;            
            this.helperClass = helperClass;
            this.graph = graph;
            this.methodCfg = methodCfg;
            this.debugging = debugging;

            // add local random generator variable
            this.tempRandomLocal = new LocalDefinition();
            this.tempRandomLocal.IsReference = false;
            this.tempRandomLocal.IsPinned = false;
            this.tempRandomLocal.IsModified = false;
            this.tempRandomLocal.Type = this.helperClass.systemRandom;
            this.tempRandomLocal.MethodDefinition = methodCfg.method;
            cfgManipulator.addLocalVariable(this.tempRandomLocal);


        }


        // this function generates code to initialize the random state generator
        public List<IOperation> generateCodeInitializeRandomState() {

            List<IOperation> operations = new List<IOperation>();

            operations.Add(this.helperClass.createNewOperation(OperationCode.Newobj, this.helperClass.systemRandomCtor));
            operations.Add(this.helperClass.createNewOperation(OperationCode.Stloc, this.tempRandomLocal));

            return operations;
        }


        // this function generates code that sets the random state
        public List<IOperation> generateCodeSetRandomState(LocalDefinition intStateLocal) {

            List<IOperation> operations = new List<IOperation>();

            operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, this.tempRandomLocal));
            operations.Add(this.helperClass.createNewOperation(OperationCode.Ldc_I4, this.graph.graphValidPathCount));
            operations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.helperClass.systemRandomNext));
            operations.Add(this.helperClass.createNewOperation(OperationCode.Stloc, intStateLocal));


            // if debugging is activated => print used global state
            if (this.debugging) {

                operations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, "Current State: "));
                operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloca, intStateLocal));
                operations.Add(this.helperClass.createNewOperation(OperationCode.Call, this.helperClass.int32ToString));
                operations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, " (possible: 0 - " + (this.graph.graphValidPathCount - 1).ToString() + ")"));
                operations.Add(this.helperClass.createNewOperation(OperationCode.Call, this.helperClass.stringConcatThree));
                operations.Add(this.helperClass.createNewOperation(OperationCode.Call, this.helperClass.systemConsoleWriteLine));

            }

            return operations;
        }

    }



    public class GraphTransformer {

        Log.Log logger = null;
        PeReader.DefaultHost host = null;
        IModule module = null;
        Helper helperClass = null;
        Random prng = null;
        CfgBuilder cfgBuilder = null;
        NamespaceTypeDefinition nodeInterface = null;

        public int duplicateBasicBlockWeight = 10;
        public int duplicateBasicBlockCorrectionValue = 2;
        public int stateChangeWeight = 10;
        public int stateChangeCorrectionValue = 2;
        public int insertOpaquePredicateWeight = 10;

        MethodDefinition interfaceStartPointerGet = null;
        MethodDefinition interfaceStartPointerSet = null;
        MethodDefinition interfaceChildNodesGet = null;
        MethodDefinition interfaceChildNodesSet = null;

        MethodDefinition buildGraphMethod = null;
        MethodDefinition exchangeNodesMethod = null;

        NamespaceTypeDefinition targetClass = null;

        Graph.Graph graph = null;
        public int graphDepth;
        public int graphDimension;

        public bool trace = false;
        public bool graphOnlyDumpVPaths = true;
        bool debugging = false;
        int debuggingNumber;
        MethodDefinition debuggingInterfaceIdGet = null;
        MethodDefinition debuggingDumpGraphMethod = null;
        FieldDefinition debuggingTraceWriter = null;

        public String debuggingDumpLocation = @"c:\";
        String debuggingDumpFilePrefix = "debug_dump_";        
        String debuggingTraceFilePrefix = "debug_trace_";

        // Hashtable of Lists of classes
        // Key: (ITypeReference) interface
        // Value: (List<struct possibleNode>) classes that implement this interface
        public Hashtable graphInterfaces = new Hashtable();


        public GraphTransformer(IModule module, PeReader.DefaultHost host, Log.Log logger, Random prng, IUnitNamespace targetNamespace, NamespaceTypeDefinition targetClass, int depth, int dimension, bool debugging = false) {
            this.host = host;
            this.logger = logger;
            this.module = module;
            this.prng = prng;
            this.helperClass = new Helper(module, host, logger);
            this.cfgBuilder = new Cfg.CfgBuilder(module, host, logger);

            this.targetClass = targetClass;

            this.graphDepth = depth;
            this.graphDimension = dimension;

            this.debugging = debugging;
            this.debuggingNumber = 0;

            // create iNode interface
            this.createNodeInterface(targetNamespace);

            // add created iNode interface to the given target class
            this.addNodeInterfaceToTargetClass(this.targetClass);
        }


        private void createNodeInterface(IUnitNamespace targetNamespace) {

            this.nodeInterface = this.helperClass.createNewInterface("iNode", targetNamespace); // TODO: interface name

            // create getter and setter for the startPointer variable
            this.interfaceStartPointerGet = this.helperClass.createNewMethod("startPointer_get", this.nodeInterface, this.nodeInterface, TypeMemberVisibility.Public, null, CallingConvention.HasThis, false, true, true); // TODO: method name

            List<IParameterDefinition> parameters = new List<IParameterDefinition>();
            ParameterDefinition parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Name = host.NameTable.GetNameFor("value");
            parameter.Type = this.nodeInterface;
            parameters.Add(parameter);
            this.interfaceStartPointerSet = this.helperClass.createNewMethod("startPointer_set", this.nodeInterface, host.PlatformType.SystemVoid, TypeMemberVisibility.Public, parameters, CallingConvention.HasThis, false, true, true); // TODO: method name

            // create getter and setter for the childNodes array variable
            parameters = new List<IParameterDefinition>();
            // int parameter
            parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Type = this.host.PlatformType.SystemInt32;
            parameters.Add(parameter);
            this.interfaceChildNodesGet = this.helperClass.createNewMethod("childNodes_get", this.nodeInterface, this.nodeInterface, TypeMemberVisibility.Public, parameters, CallingConvention.HasThis, false, true, true); // TODO: method name

            parameters = new List<IParameterDefinition>();
            // iNode parameter
            parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Type = this.nodeInterface;
            parameters.Add(parameter);
            // int parameter
            parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Type = this.host.PlatformType.SystemInt32;
            parameters.Add(parameter);
            this.interfaceChildNodesSet = this.helperClass.createNewMethod("childNodes_set", this.nodeInterface, host.PlatformType.SystemVoid, TypeMemberVisibility.Public, parameters, CallingConvention.HasThis, false, true, true); // TODO: method name

            // when debugging is activated
            // => create getter for the id variable that distinct identifies the object
            if (this.debugging) {

                this.logger.writeLine("Debugging activated: Adding getter for unique object id to node interface");

                this.debuggingInterfaceIdGet = this.helperClass.createNewMethod("DEBUG_objectId_get", this.nodeInterface, this.host.PlatformType.SystemString, TypeMemberVisibility.Public, null, CallingConvention.HasThis, false, true, true); // TODO: method name
            }

        }


        public void generateGraph(int graphValidPathCount) {

            this.graph = new Graph.Graph(this.graphInterfaces, this.prng, this.graphDepth, this.graphDimension);

            ValidGraphPath[] validPaths = this.graph.generateValidPaths(graphValidPathCount);

            // build graph from the given paths
            this.graph.buildGraph(validPaths);

            if (this.debugging) {
                this.logger.dumpGraph(this.graph.startingNode, "paths", this.graphOnlyDumpVPaths);
            }
        }


        // this function adds the created iNode interface to the class and also implements all neded functions
        public void addNodeInterfaceToTargetClass(NamespaceTypeDefinition givenClass) {

            this.logger.writeLine("");
            this.logger.writeLine("Add node interface \"" + this.nodeInterface.ToString() + "\" to class \"" + givenClass.ToString() + "\"");

            // check if generated interface was already added to target class
            if (givenClass.Interfaces != null
                && givenClass.Interfaces.Contains(this.nodeInterface)) {
                this.logger.writeLine("Class \"" + givenClass.ToString() + "\" already contains node interface \"" + this.nodeInterface.ToString() + "\"");
                return;
            }

            // add nodes interface to class
            if (givenClass.Interfaces != null) {
                givenClass.Interfaces.Add(this.nodeInterface);
            }
            else {
                givenClass.Interfaces = new List<ITypeReference>();
                givenClass.Interfaces.Add(this.nodeInterface);
            }

            // add start pointer field
            FieldDefinition startPointerField = new FieldDefinition();
            startPointerField.IsCompileTimeConstant = false;
            startPointerField.IsNotSerialized = false;
            startPointerField.IsReadOnly = false;
            startPointerField.IsRuntimeSpecial = false;
            startPointerField.IsSpecialName = false;
            startPointerField.Type = this.nodeInterface;
            startPointerField.IsStatic = false;
            startPointerField.Visibility = TypeMemberVisibility.Public;
            startPointerField.Name = host.NameTable.GetNameFor("startPointer");
            startPointerField.InternFactory = host.InternFactory;
            startPointerField.ContainingTypeDefinition = givenClass;
            if (givenClass.Fields != null) {
                givenClass.Fields.Add(startPointerField);
            }
            else {
                givenClass.Fields = new List<IFieldDefinition>();
                givenClass.Fields.Add(startPointerField);
            }

            // create getter for the startPointer variable
            MethodDefinition startPointerGetMethod = this.helperClass.createNewMethod("startPointer_get", givenClass, this.nodeInterface, TypeMemberVisibility.Public, null, CallingConvention.HasThis, false, false, true); // TODO: method name

            // create a body for the getter method
            ILGenerator ilGenerator = new ILGenerator(host, startPointerGetMethod);
            ilGenerator.Emit(OperationCode.Ldarg_0);
            ilGenerator.Emit(OperationCode.Ldfld, startPointerField);
            ilGenerator.Emit(OperationCode.Ret);
            IMethodBody body = new ILGeneratorMethodBody(ilGenerator, true, 1, startPointerGetMethod, Enumerable<ILocalDefinition>.Empty, Enumerable<ITypeDefinition>.Empty);
            startPointerGetMethod.Body = body;

            // create setter for the startPointer variable
            List<IParameterDefinition> parameters = new List<IParameterDefinition>();
            ParameterDefinition parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Name = host.NameTable.GetNameFor("value");
            parameter.Type = this.nodeInterface;
            parameters.Add(parameter);
            MethodDefinition startPointerSetMethod = this.helperClass.createNewMethod("startPointer_set", givenClass, host.PlatformType.SystemVoid, TypeMemberVisibility.Public, parameters, CallingConvention.HasThis, false, false, true); // TODO: method name

            // create a body for the setter method
            ilGenerator = new ILGenerator(host, startPointerSetMethod);
            ilGenerator.Emit(OperationCode.Ldarg_0);
            ilGenerator.Emit(OperationCode.Ldarg_1);
            ilGenerator.Emit(OperationCode.Stfld, startPointerField);
            ilGenerator.Emit(OperationCode.Ret);
            body = new ILGeneratorMethodBody(ilGenerator, true, 1, startPointerSetMethod, Enumerable<ILocalDefinition>.Empty, Enumerable<ITypeDefinition>.Empty);
            startPointerSetMethod.Body = body;

            // add childNodes array field
            FieldDefinition childNodesField = new FieldDefinition();
            childNodesField.IsCompileTimeConstant = false;
            childNodesField.IsNotSerialized = false;
            childNodesField.IsReadOnly = false;
            childNodesField.IsRuntimeSpecial = false;
            childNodesField.IsSpecialName = false;
            // create array of node interface type
            VectorTypeReference nodeInterfaceArrayType = new VectorTypeReference();
            nodeInterfaceArrayType.ElementType = this.nodeInterface;
            nodeInterfaceArrayType.Rank = 1;
            nodeInterfaceArrayType.IsFrozen = true;
            nodeInterfaceArrayType.IsValueType = false;
            nodeInterfaceArrayType.InternFactory = host.InternFactory;
            childNodesField.Type = nodeInterfaceArrayType;

            childNodesField.IsStatic = false;
            childNodesField.Visibility = TypeMemberVisibility.Public;
            childNodesField.Name = host.NameTable.GetNameFor("childNodes");
            childNodesField.InternFactory = host.InternFactory;
            childNodesField.ContainingTypeDefinition = givenClass;
            givenClass.Fields.Add(childNodesField);

            // create getter for the childNodes variable
            parameters = new List<IParameterDefinition>();
            // int parameter
            parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Type = this.host.PlatformType.SystemInt32;
            parameters.Add(parameter);
            MethodDefinition childNodesGetMethod = this.helperClass.createNewMethod("childNodes_get", givenClass, this.nodeInterface, TypeMemberVisibility.Public, parameters, CallingConvention.HasThis, false, false, true); // TODO: method name

            // create a body for the getter method
            ilGenerator = new ILGenerator(host, childNodesGetMethod);
            ilGenerator.Emit(OperationCode.Ldarg_0);
            ilGenerator.Emit(OperationCode.Ldfld, childNodesField);
            ilGenerator.Emit(OperationCode.Ldarg_1);
            ilGenerator.Emit(OperationCode.Ldelem_Ref);
            ilGenerator.Emit(OperationCode.Ret);
            body = new ILGeneratorMethodBody(ilGenerator, true, 1, childNodesGetMethod, Enumerable<ILocalDefinition>.Empty, Enumerable<ITypeDefinition>.Empty);
            childNodesGetMethod.Body = body;

            // create setter for the childNodes variable
            parameters = new List<IParameterDefinition>();
            // iNode parameter
            parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Type = this.nodeInterface;
            parameters.Add(parameter);
            // int parameter
            parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Type = this.host.PlatformType.SystemInt32;
            parameters.Add(parameter);
            MethodDefinition childNodesSetMethod = this.helperClass.createNewMethod("childNodes_set", givenClass, host.PlatformType.SystemVoid, TypeMemberVisibility.Public, parameters, CallingConvention.HasThis, false, false, true); // TODO: method name

            // create a body for the setter method
            ilGenerator = new ILGenerator(host, childNodesSetMethod);
            ilGenerator.Emit(OperationCode.Ldarg_0);
            ilGenerator.Emit(OperationCode.Ldfld, childNodesField);
            ilGenerator.Emit(OperationCode.Ldarg_2);
            ilGenerator.Emit(OperationCode.Ldarg_1);
            ilGenerator.Emit(OperationCode.Stelem_Ref);
            ilGenerator.Emit(OperationCode.Ret);
            body = new ILGeneratorMethodBody(ilGenerator, true, 1, childNodesSetMethod, Enumerable<ILocalDefinition>.Empty, Enumerable<ITypeDefinition>.Empty);
            childNodesSetMethod.Body = body;

            // add current node field
            FieldDefinition currentNodeField = new FieldDefinition();
            currentNodeField.IsCompileTimeConstant = false;
            currentNodeField.IsNotSerialized = false;
            currentNodeField.IsReadOnly = false;
            currentNodeField.IsRuntimeSpecial = false;
            currentNodeField.IsSpecialName = false;
            currentNodeField.Type = this.nodeInterface;
            currentNodeField.IsStatic = false;
            currentNodeField.Visibility = TypeMemberVisibility.Public;
            currentNodeField.Name = host.NameTable.GetNameFor("currentNode");
            currentNodeField.InternFactory = host.InternFactory;
            currentNodeField.ContainingTypeDefinition = givenClass;
            givenClass.Fields.Add(currentNodeField);

            // create getter for the currentNode variable
            MethodDefinition currentNodeGetMethod = this.helperClass.createNewMethod("currentNode_get", givenClass, this.nodeInterface, TypeMemberVisibility.Public, null, CallingConvention.HasThis, false, false, true); // TODO: method name

            // create a body for the getter method
            ilGenerator = new ILGenerator(host, currentNodeGetMethod);
            ilGenerator.Emit(OperationCode.Ldarg_0);
            ilGenerator.Emit(OperationCode.Ldfld, currentNodeField);
            ilGenerator.Emit(OperationCode.Ret);
            body = new ILGeneratorMethodBody(ilGenerator, true, 1, currentNodeGetMethod, Enumerable<ILocalDefinition>.Empty, Enumerable<ITypeDefinition>.Empty);
            currentNodeGetMethod.Body = body;

            // create setter for the currentNode variable
            parameters = new List<IParameterDefinition>();
            parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Name = host.NameTable.GetNameFor("value");
            parameter.Type = this.nodeInterface;
            parameters.Add(parameter);
            MethodDefinition currentNodeSetMethod = this.helperClass.createNewMethod("currentNode_set", givenClass, host.PlatformType.SystemVoid, TypeMemberVisibility.Public, parameters, CallingConvention.HasThis, false, false, true); // TODO: method name

            // create a body for the setter method
            ilGenerator = new ILGenerator(host, currentNodeSetMethod);
            ilGenerator.Emit(OperationCode.Ldarg_0);
            ilGenerator.Emit(OperationCode.Ldarg_1);
            ilGenerator.Emit(OperationCode.Stfld, currentNodeField);
            ilGenerator.Emit(OperationCode.Ret);
            body = new ILGeneratorMethodBody(ilGenerator, true, 1, currentNodeSetMethod, Enumerable<ILocalDefinition>.Empty, Enumerable<ITypeDefinition>.Empty);
            currentNodeSetMethod.Body = body;

            // check if debugging is activated
            // => add graph debugging attributes and methods
            MethodDefinition objectIdGetMethod = null;
            FieldDefinition objectIdField = null;
            if (this.debugging) {

                this.logger.writeLine("Debugging activated: Adding field for unique object id and getter method");

                // add objectId field
                objectIdField = new FieldDefinition();
                objectIdField.IsCompileTimeConstant = false;
                objectIdField.IsNotSerialized = false;
                objectIdField.IsReadOnly = false;
                objectIdField.IsRuntimeSpecial = false;
                objectIdField.IsSpecialName = false;
                objectIdField.Type = this.host.PlatformType.SystemString;
                objectIdField.IsStatic = false;
                objectIdField.Name = host.NameTable.GetNameFor("DEBUG_objectId");
                objectIdField.Visibility = TypeMemberVisibility.Public;
                objectIdField.InternFactory = host.InternFactory;
                objectIdField.ContainingTypeDefinition = givenClass;
                givenClass.Fields.Add(objectIdField);

                // create getter for the objectId variable
                objectIdGetMethod = this.helperClass.createNewMethod("DEBUG_objectId_get", givenClass, this.host.PlatformType.SystemString, TypeMemberVisibility.Public, null, CallingConvention.HasThis, false, false, true); // TODO: method name

                // create a body for the getter method
                ilGenerator = new ILGenerator(host, objectIdGetMethod);
                ilGenerator.Emit(OperationCode.Ldarg_0);
                ilGenerator.Emit(OperationCode.Ldfld, objectIdField);
                ilGenerator.Emit(OperationCode.Ret);
                body = new ILGeneratorMethodBody(ilGenerator, true, 1, objectIdGetMethod, Enumerable<ILocalDefinition>.Empty, Enumerable<ITypeDefinition>.Empty);
                objectIdGetMethod.Body = body;

            }

            // add .ctor(iNode) to target class
            parameters = new List<IParameterDefinition>();
            parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Type = this.nodeInterface;
            parameters.Add(parameter);

            MethodDefinition nodeConstructor = this.helperClass.createNewMethod(".ctor", givenClass, host.PlatformType.SystemVoid, TypeMemberVisibility.Public, parameters, CallingConvention.HasThis, false, false, false);
            nodeConstructor.IsSpecialName = true;
            nodeConstructor.IsHiddenBySignature = true;
            nodeConstructor.IsRuntimeSpecial = true;

            // generate node constructor body
            ilGenerator = new ILGenerator(host, currentNodeSetMethod);

            // create local variable list
            List<ILocalDefinition> localVariables = new List<ILocalDefinition>();

            // call system.object constructor
            ilGenerator.Emit(OperationCode.Ldarg_0);
            ilGenerator.Emit(OperationCode.Call, this.helperClass.objectCtor);

            // initialize childNodes array
            ilGenerator.Emit(OperationCode.Ldarg_0);
            ilGenerator.Emit(OperationCode.Ldc_I4, this.graphDimension);
            ilGenerator.Emit(OperationCode.Newarr, nodeInterfaceArrayType);
            ilGenerator.Emit(OperationCode.Stfld, childNodesField);

            // check if debugging is activated
            // => add code to constructor that generates a unique id for this object
            if (this.debugging) {

                this.logger.writeLine("Debugging activated: Adding code to generate unique id to node constructor");

                // create local integer variable needed for debugging code
                LocalDefinition intLocal = new LocalDefinition();
                intLocal.IsReference = false;
                intLocal.IsPinned = false;
                intLocal.IsModified = false;
                intLocal.Type = this.host.PlatformType.SystemInt32;
                intLocal.MethodDefinition = nodeConstructor;
                localVariables.Add(intLocal);

                // use the hash code of this instance as unique object id
                ilGenerator.Emit(OperationCode.Ldarg_0);
                ilGenerator.Emit(OperationCode.Ldarg_0);
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.objectGetHashCode);
                ilGenerator.Emit(OperationCode.Stloc, intLocal);

                // cast random integer to string and store in object id
                ilGenerator.Emit(OperationCode.Ldloca, intLocal);
                ilGenerator.Emit(OperationCode.Call, this.helperClass.int32ToString);
                ilGenerator.Emit(OperationCode.Stfld, objectIdField);

            }

            ilGenerator.Emit(OperationCode.Ret);
            body = new ILGeneratorMethodBody(ilGenerator, true, 8, nodeConstructor, localVariables, Enumerable<ITypeDefinition>.Empty);
            nodeConstructor.Body = body;


            // add class to the list of possible nodes for the graph
            PossibleNode tempStruct = new PossibleNode();
            tempStruct.nodeConstructor = nodeConstructor;
            tempStruct.givenClass = givenClass;

            // for each interface the class implements add it to the according list
            foreach (ITypeReference interfaceRef in givenClass.Interfaces) {

                this.logger.writeLine("Add class \"" + givenClass.ToString() + "\" to graph interface list \"" + interfaceRef.ToString() + "\"");

                // check if a list with this interface already exists
                // => if it does just add current class to list
                if (this.graphInterfaces.ContainsKey(interfaceRef)) {
                    List<PossibleNode> tempList = (List<PossibleNode>)this.graphInterfaces[interfaceRef];
                    tempList.Add(tempStruct);
                }
                // if not => add new list for this interface
                else {
                    List<PossibleNode> tempList = new List<PossibleNode>();
                    tempList.Add(tempStruct);
                    this.graphInterfaces.Add(interfaceRef, tempList);
                }
            }

            this.logger.writeLine("");

        }


        // this function creates all methods that are needed for the generated graph
        public void createGraphMethods() {

            this.logger.writeLine("Adding graph methods to \"" + this.targetClass.ToString() + "\"");

            // check if the graph is already initialized (needed to create the methods)
            if (this.graph == null) {
                throw new ArgumentException("Graph is not initialized.");
            }

            // if debugging is activated
            // => add field for the basic block trace file
            if (this.debugging
                || this.trace) {

                this.logger.writeLine("Debugging activated: Adding field for a basic block tracer file");

                // add trace writer field
                this.debuggingTraceWriter = new FieldDefinition();
                this.debuggingTraceWriter.IsCompileTimeConstant = false;
                this.debuggingTraceWriter.IsNotSerialized = false;
                this.debuggingTraceWriter.IsReadOnly = false;
                this.debuggingTraceWriter.IsRuntimeSpecial = false;
                this.debuggingTraceWriter.IsSpecialName = false;
                this.debuggingTraceWriter.Type = this.helperClass.systemIOStreamWriter;
                this.debuggingTraceWriter.IsStatic = false;
                this.debuggingTraceWriter.Name = host.NameTable.GetNameFor("DEBUG_traceWriter");
                this.debuggingTraceWriter.Visibility = TypeMemberVisibility.Public;
                this.debuggingTraceWriter.InternFactory = host.InternFactory;
                this.debuggingTraceWriter.ContainingTypeDefinition = this.targetClass;
                this.targetClass.Fields.Add(this.debuggingTraceWriter);

            }

            // create a method that can be called to generate the graph
            this.buildGraphMethod = this.helperClass.createNewMethod("buildGraph", this.targetClass, host.PlatformType.SystemVoid, TypeMemberVisibility.Public, null, CallingConvention.HasThis, false, false, true); // TODO RENAME

            ILGenerator ilGenerator = new ILGenerator(host, this.buildGraphMethod);

            // check if graph was already build
            // => if it was jump to exit
            ILGeneratorLabel buildGraphExitLabel = new ILGeneratorLabel();
            ilGenerator.Emit(OperationCode.Ldarg_0);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceStartPointerGet);
            ilGenerator.Emit(OperationCode.Ldnull);
            ilGenerator.Emit(OperationCode.Ceq);
            ilGenerator.Emit(OperationCode.Brfalse, buildGraphExitLabel);
            
            // set initial node (root of the tree)
            ilGenerator.Emit(OperationCode.Ldarg_0);
            ilGenerator.Emit(OperationCode.Ldarg_0); // needed as argument for the constructor
            ilGenerator.Emit(OperationCode.Newobj, this.graph.startingNode.constructorToUse);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceStartPointerSet);

            // build rest of graph in a "pseudo recursive" manner
            MethodDefinition newMethodToCall = this.addNodeRecursively(this.graph.startingNode, 1, "addNode_0"); // TODO: method name
            ilGenerator.Emit(OperationCode.Ldarg_0);
            ilGenerator.Emit(OperationCode.Ldarg_0);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceStartPointerGet);
            ilGenerator.Emit(OperationCode.Ldc_I4_1);
            ilGenerator.Emit(OperationCode.Callvirt, newMethodToCall);

            // exit
            ilGenerator.MarkLabel(buildGraphExitLabel);
            ilGenerator.Emit(OperationCode.Ret);

            // create body
            IMethodBody body = new ILGeneratorMethodBody(ilGenerator, true, 8, this.buildGraphMethod, Enumerable<ILocalDefinition>.Empty, Enumerable<ITypeDefinition>.Empty);
            this.buildGraphMethod.Body = body;

            // create exchangeNodes method
            List<IParameterDefinition> parameters = new List<IParameterDefinition>();
            // node type parameter
            ParameterDefinition parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Type = this.nodeInterface;
            parameters.Add(parameter);
            // int array parameter for path to node one
            VectorTypeReference intArrayType = new VectorTypeReference();
            intArrayType.ElementType = this.host.PlatformType.SystemInt32;
            intArrayType.Rank = 1;
            intArrayType.IsFrozen = true;
            intArrayType.IsValueType = false;
            intArrayType.InternFactory = host.InternFactory;
            parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Type = intArrayType;
            parameters.Add(parameter);
            // int array parameter for path to node two
            parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Type = intArrayType;
            parameters.Add(parameter);

            this.exchangeNodesMethod = this.helperClass.createNewMethod("exchangeNodes", this.targetClass, host.PlatformType.SystemVoid, TypeMemberVisibility.Public, parameters, CallingConvention.HasThis, false, false, true); // TODO RENAME

            ilGenerator = new ILGenerator(host, this.exchangeNodesMethod);
            List<ILocalDefinition> localVariables = new List<ILocalDefinition>();

            // create local integer variable needed for the loops
            LocalDefinition loopIntLocal = new LocalDefinition();
            loopIntLocal.IsReference = false;
            loopIntLocal.IsPinned = false;
            loopIntLocal.IsModified = false;
            loopIntLocal.Type = this.host.PlatformType.SystemInt32;
            loopIntLocal.MethodDefinition = this.exchangeNodesMethod;
            localVariables.Add(loopIntLocal);

            // create local iNode variable needed for nodeOne
            LocalDefinition nodeOneLocal = new LocalDefinition();
            nodeOneLocal.IsReference = false;
            nodeOneLocal.IsPinned = false;
            nodeOneLocal.IsModified = false;
            nodeOneLocal.Type = this.nodeInterface;
            nodeOneLocal.MethodDefinition = this.exchangeNodesMethod;
            localVariables.Add(nodeOneLocal);

            // create local iNode variable needed for prevNodeOne
            LocalDefinition prevNodeOneLocal = new LocalDefinition();
            prevNodeOneLocal.IsReference = false;
            prevNodeOneLocal.IsPinned = false;
            prevNodeOneLocal.IsModified = false;
            prevNodeOneLocal.Type = this.nodeInterface;
            prevNodeOneLocal.MethodDefinition = this.exchangeNodesMethod;
            localVariables.Add(prevNodeOneLocal);

            // create local integer variable needed for prevNodeOneIdx
            LocalDefinition prevNodeOneIdxLocal = new LocalDefinition();
            prevNodeOneIdxLocal.IsReference = false;
            prevNodeOneIdxLocal.IsPinned = false;
            prevNodeOneIdxLocal.IsModified = false;
            prevNodeOneIdxLocal.Type = this.host.PlatformType.SystemInt32;
            prevNodeOneIdxLocal.MethodDefinition = this.exchangeNodesMethod;
            localVariables.Add(prevNodeOneIdxLocal);

            // create local iNode variable needed for nodeTwo
            LocalDefinition nodeTwoLocal = new LocalDefinition();
            nodeTwoLocal.IsReference = false;
            nodeTwoLocal.IsPinned = false;
            nodeTwoLocal.IsModified = false;
            nodeTwoLocal.Type = this.nodeInterface;
            nodeTwoLocal.MethodDefinition = this.exchangeNodesMethod;
            localVariables.Add(nodeTwoLocal);

            // create local iNode variable needed for prevNodeOne
            LocalDefinition prevNodeTwoLocal = new LocalDefinition();
            prevNodeTwoLocal.IsReference = false;
            prevNodeTwoLocal.IsPinned = false;
            prevNodeTwoLocal.IsModified = false;
            prevNodeTwoLocal.Type = this.nodeInterface;
            prevNodeTwoLocal.MethodDefinition = this.exchangeNodesMethod;
            localVariables.Add(prevNodeTwoLocal);

            // create local integer variable needed for prevNodeOneIdx
            LocalDefinition prevNodeTwoIdxLocal = new LocalDefinition();
            prevNodeTwoIdxLocal.IsReference = false;
            prevNodeTwoIdxLocal.IsPinned = false;
            prevNodeTwoIdxLocal.IsModified = false;
            prevNodeTwoIdxLocal.Type = this.host.PlatformType.SystemInt32;
            prevNodeTwoIdxLocal.MethodDefinition = this.exchangeNodesMethod;
            localVariables.Add(prevNodeTwoIdxLocal);

            // create local iNode variable needed for temp
            LocalDefinition tempNodeLocal = new LocalDefinition();
            tempNodeLocal.IsReference = false;
            tempNodeLocal.IsPinned = false;
            tempNodeLocal.IsModified = false;
            tempNodeLocal.Type = this.nodeInterface;
            tempNodeLocal.MethodDefinition = this.exchangeNodesMethod;
            localVariables.Add(tempNodeLocal);

            // initialize local variables
            /*
            iNode nodeOne = givenStartingNode;
            iNode prevNodeOne = null;
            int prevNodeOneIdx = 0;
            */
            ilGenerator.Emit(OperationCode.Ldarg_1);
            ilGenerator.Emit(OperationCode.Stloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldnull);
            ilGenerator.Emit(OperationCode.Stloc, prevNodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldc_I4_0);
            ilGenerator.Emit(OperationCode.Stloc, prevNodeOneIdxLocal);

            // initialize loop
            ilGenerator.Emit(OperationCode.Ldc_I4_0);
            ilGenerator.Emit(OperationCode.Stloc, loopIntLocal);
            ILGeneratorLabel loopConditionAndIncBranch = new ILGeneratorLabel();
            ILGeneratorLabel loopConditionBranch = new ILGeneratorLabel();
            ILGeneratorLabel loopStartBranch = new ILGeneratorLabel();
            ilGenerator.Emit(OperationCode.Br, loopConditionBranch);

            // start of the code in the loop
            /*
            if (nodeOne.getNode(pathToNodeOne[i]) != null) {
            */
            ilGenerator.MarkLabel(loopStartBranch);
            ilGenerator.Emit(OperationCode.Ldloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldarg_2);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Ldelem_I4);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);
            ilGenerator.Emit(OperationCode.Ldnull);
            ilGenerator.Emit(OperationCode.Ceq);
            ilGenerator.Emit(OperationCode.Brtrue, loopConditionAndIncBranch);

            // get the node of the graph that should be exchanged (nodeOne)
            /*
            prevNodeOne = nodeOne;
            prevNodeOneIdx = pathToNodeOne[i];
            nodeOne = nodeOne.getNode(pathToNodeOne[i]);
            */
            ilGenerator.Emit(OperationCode.Ldloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Stloc, prevNodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldarg_2);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Ldelem_I4);
            ilGenerator.Emit(OperationCode.Stloc, prevNodeOneIdxLocal);
            ilGenerator.Emit(OperationCode.Ldloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldloc, prevNodeOneIdxLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);
            ilGenerator.Emit(OperationCode.Stloc, nodeOneLocal);

            // increment counter of loop
            ilGenerator.MarkLabel(loopConditionAndIncBranch);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Ldc_I4_1);
            ilGenerator.Emit(OperationCode.Add);
            ilGenerator.Emit(OperationCode.Stloc, loopIntLocal);

            // loop condition
            /*
            for (int i = 0; i < pathToNodeOne.Length; i++) {
            */
            ilGenerator.MarkLabel(loopConditionBranch);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Ldarg_2);
            ilGenerator.Emit(OperationCode.Ldlen);
            ilGenerator.Emit(OperationCode.Conv_I4);
            ilGenerator.Emit(OperationCode.Clt);
            ilGenerator.Emit(OperationCode.Brtrue, loopStartBranch);

            // initialize local variables
            /*
            iNode nodeTwo = givenStartingNode;
            iNode prevNodeTwo = null;
            int prevNodeTwoIdx = 0;
            */
            ilGenerator.Emit(OperationCode.Ldarg_1);
            ilGenerator.Emit(OperationCode.Stloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldnull);
            ilGenerator.Emit(OperationCode.Stloc, prevNodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldc_I4_0);
            ilGenerator.Emit(OperationCode.Stloc, prevNodeTwoIdxLocal);

            // initialize loop
            ilGenerator.Emit(OperationCode.Ldc_I4_0);
            ilGenerator.Emit(OperationCode.Stloc, loopIntLocal);
            loopConditionAndIncBranch = new ILGeneratorLabel();
            loopConditionBranch = new ILGeneratorLabel();
            loopStartBranch = new ILGeneratorLabel();
            ilGenerator.Emit(OperationCode.Br, loopConditionBranch);

            // start of the code in the loop
            /*
            if (nodeTwo.getNode(pathToNodeTwo[i]) != null) {
            */
            ilGenerator.MarkLabel(loopStartBranch);
            ilGenerator.Emit(OperationCode.Ldloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldarg_3);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Ldelem_I4);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);
            ilGenerator.Emit(OperationCode.Ldnull);
            ilGenerator.Emit(OperationCode.Ceq);
            ilGenerator.Emit(OperationCode.Brtrue, loopConditionAndIncBranch);

            // get the node of the graph that should be exchanged (nodeTwo)
            /*
            prevNodeTwo = nodeTwo;
            prevNodeTwoIdx = pathToNodeTwo[i];
            nodeTwo = nodeTwo.getNode(pathToNodeTwo[i]);
            */
            ilGenerator.Emit(OperationCode.Ldloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Stloc, prevNodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldarg_2);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Ldelem_I4);
            ilGenerator.Emit(OperationCode.Stloc, prevNodeTwoIdxLocal);
            ilGenerator.Emit(OperationCode.Ldloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldloc, prevNodeTwoIdxLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);
            ilGenerator.Emit(OperationCode.Stloc, nodeTwoLocal);

            // increment counter of loop
            ilGenerator.MarkLabel(loopConditionAndIncBranch);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Ldc_I4_1);
            ilGenerator.Emit(OperationCode.Add);
            ilGenerator.Emit(OperationCode.Stloc, loopIntLocal);

            // loop condition
            /*
            for (int i = 0; i < pathToNodeTwo.Length; i++) {
            */
            ilGenerator.MarkLabel(loopConditionBranch);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Ldarg_3);
            ilGenerator.Emit(OperationCode.Ldlen);
            ilGenerator.Emit(OperationCode.Conv_I4);
            ilGenerator.Emit(OperationCode.Clt);
            ilGenerator.Emit(OperationCode.Brtrue, loopStartBranch);

            // initialize loop
            ilGenerator.Emit(OperationCode.Ldc_I4_0);
            ilGenerator.Emit(OperationCode.Stloc, loopIntLocal);
            loopConditionAndIncBranch = new ILGeneratorLabel();
            loopConditionBranch = new ILGeneratorLabel();
            loopStartBranch = new ILGeneratorLabel();
            ilGenerator.Emit(OperationCode.Br, loopConditionBranch);

            // start of the code in the loop
            ilGenerator.MarkLabel(loopStartBranch);

            /*
            if (nodeOne.getNode(i) == nodeTwo) {
            */
            ILGeneratorLabel conditionBranch = new ILGeneratorLabel();
            ilGenerator.Emit(OperationCode.Ldloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);
            ilGenerator.Emit(OperationCode.Ldloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ceq);
            ilGenerator.Emit(OperationCode.Brfalse, conditionBranch);

            /*
            nodeOne.setNode(nodeTwo.getNode(i), i);
            */
            ilGenerator.Emit(OperationCode.Ldloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesSet);

            /*
            nodeTwo.setNode(nodeOne, i);
            */
            ilGenerator.Emit(OperationCode.Ldloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesSet);
            ilGenerator.Emit(OperationCode.Br, loopConditionAndIncBranch);

            /*
            else if (nodeTwo.getNode(i) == nodeOne) {
            */
            ilGenerator.MarkLabel(conditionBranch);
            ilGenerator.Emit(OperationCode.Ldloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);
            ilGenerator.Emit(OperationCode.Ldloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Ceq);
            conditionBranch = new ILGeneratorLabel();
            ilGenerator.Emit(OperationCode.Brfalse, conditionBranch);

            /*
            nodeTwo.setNode(nodeOne.getNode(i), i);
            */
            ilGenerator.Emit(OperationCode.Ldloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesSet);

            /*
            nodeOne.setNode(nodeTwo, i);
            */
            ilGenerator.Emit(OperationCode.Ldloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesSet);
            ilGenerator.Emit(OperationCode.Br, loopConditionAndIncBranch);

            /*
            else {
            */
            ilGenerator.MarkLabel(conditionBranch);

            /*
            temp = nodeOne.getNode(i);
            */
            ilGenerator.Emit(OperationCode.Ldloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);
            ilGenerator.Emit(OperationCode.Stloc, tempNodeLocal);

            /*
            nodeOne.setNode(nodeTwo.getNode(i), i);
            */
            ilGenerator.Emit(OperationCode.Ldloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesSet);

            /*
            nodeTwo.setNode(temp, i);
            */
            ilGenerator.Emit(OperationCode.Ldloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldloc, tempNodeLocal);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesSet);

            // increment counter of loop
            ilGenerator.MarkLabel(loopConditionAndIncBranch);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Ldc_I4_1);
            ilGenerator.Emit(OperationCode.Add);
            ilGenerator.Emit(OperationCode.Stloc, loopIntLocal);

            // loop condition
            /*
            for (int i = 0; i < GRAPH_DIMENSION; i++) {
            */
            ilGenerator.MarkLabel(loopConditionBranch);
            ilGenerator.Emit(OperationCode.Ldloc, loopIntLocal);
            ilGenerator.Emit(OperationCode.Ldc_I4, this.graphDimension);
            ilGenerator.Emit(OperationCode.Clt);
            ilGenerator.Emit(OperationCode.Brtrue, loopStartBranch);

            /*
            if (prevNodeOne != null) {
            */
            conditionBranch = new ILGeneratorLabel();
            ilGenerator.Emit(OperationCode.Ldloc, prevNodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldnull);
            ilGenerator.Emit(OperationCode.Ceq);
            ilGenerator.Emit(OperationCode.Brtrue, conditionBranch);

            /*
            if (prevNodeOne != nodeTwo) {
            */
            ILGeneratorLabel exitConditionBranch = new ILGeneratorLabel();
            ilGenerator.Emit(OperationCode.Ldloc, prevNodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ceq);
            ilGenerator.Emit(OperationCode.Brtrue, exitConditionBranch);

            /*
            prevNodeOne.setNode(nodeTwo, prevNodeOneIdx);
            */
            ilGenerator.Emit(OperationCode.Ldloc, prevNodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldloc, prevNodeOneIdxLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesSet);
            ilGenerator.Emit(OperationCode.Br, exitConditionBranch);

            /*
            else {
            */
            ilGenerator.MarkLabel(conditionBranch);

            /*
            this.graphStartNode = nodeTwo;
            */
            ilGenerator.Emit(OperationCode.Ldarg_0);
            ilGenerator.Emit(OperationCode.Ldloc, nodeTwoLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceStartPointerSet);

            ilGenerator.MarkLabel(exitConditionBranch);

            /*
            if (prevNodeTwo != null) {
            */
            conditionBranch = new ILGeneratorLabel();
            ilGenerator.Emit(OperationCode.Ldloc, prevNodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldnull);
            ilGenerator.Emit(OperationCode.Ceq);
            ilGenerator.Emit(OperationCode.Brtrue, conditionBranch);

            /*
            if (prevNodeTwo != nodeOne) {
            */
            exitConditionBranch = new ILGeneratorLabel();
            ilGenerator.Emit(OperationCode.Ldloc, prevNodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Ceq);
            ilGenerator.Emit(OperationCode.Brtrue, exitConditionBranch);

            /*
            prevNodeTwo.setNode(nodeOne, prevNodeTwoIdx);
            */
            ilGenerator.Emit(OperationCode.Ldloc, prevNodeTwoLocal);
            ilGenerator.Emit(OperationCode.Ldloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Ldloc, prevNodeTwoIdxLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesSet);
            ilGenerator.Emit(OperationCode.Br, exitConditionBranch);

            /*
            else {
            */
            ilGenerator.MarkLabel(conditionBranch);

            /*
            this.graphStartNode = nodeOne;
            */
            ilGenerator.Emit(OperationCode.Ldarg_0);
            ilGenerator.Emit(OperationCode.Ldloc, nodeOneLocal);
            ilGenerator.Emit(OperationCode.Callvirt, this.interfaceStartPointerSet);

            ilGenerator.MarkLabel(exitConditionBranch);
            ilGenerator.Emit(OperationCode.Ret);

            // create body
            body = new ILGeneratorMethodBody(ilGenerator, true, 8, this.exchangeNodesMethod, localVariables, Enumerable<ITypeDefinition>.Empty);
            this.exchangeNodesMethod.Body = body;

            // check if debugging is activated
            // => add function to dump graph as .dot file
            if (this.debugging) {

                this.logger.writeLine("Debugging activated: Adding code to dump graph as .dot file");

                // create dumpGraph method
                parameters = new List<IParameterDefinition>();
                // string parameter
                parameter = new ParameterDefinition();
                parameter.IsIn = false;
                parameter.IsOptional = false;
                parameter.IsOut = false;
                parameter.Type = this.host.PlatformType.SystemString;
                parameter.Index = 0;
                parameters.Add(parameter);
                // node type parameter (current pointer of debug method)
                parameter = new ParameterDefinition();
                parameter.IsIn = false;
                parameter.IsOptional = false;
                parameter.IsOut = false;
                parameter.Type = this.nodeInterface;
                parameter.Index = 1;
                parameters.Add(parameter);
                // node type parameter (current pointer of caller)
                parameter = new ParameterDefinition();
                parameter.IsIn = false;
                parameter.IsOptional = false;
                parameter.IsOut = false;
                parameter.Type = this.nodeInterface;
                parameter.Index = 2;
                parameters.Add(parameter);

                this.debuggingDumpGraphMethod = this.helperClass.createNewMethod("DEBUG_dumpGraph", this.targetClass, host.PlatformType.SystemVoid, TypeMemberVisibility.Public, parameters, CallingConvention.HasThis, false, false, true);

                // create dumpGraphRec method
                parameters = new List<IParameterDefinition>();
                // stream writer parameter
                parameter = new ParameterDefinition();
                parameter.IsIn = false;
                parameter.IsOptional = false;
                parameter.IsOut = false;
                parameter.Type = this.helperClass.systemIOStreamWriter;
                parameter.Index = 0;
                parameters.Add(parameter);
                // string parameter
                parameter = new ParameterDefinition();
                parameter.IsIn = false;
                parameter.IsOptional = false;
                parameter.IsOut = false;
                parameter.Type = this.host.PlatformType.SystemString;
                parameter.Index = 1;
                parameters.Add(parameter);
                // node type parameter (current pointer of debug method)
                parameter = new ParameterDefinition();
                parameter.IsIn = false;
                parameter.IsOptional = false;
                parameter.IsOut = false;
                parameter.Type = this.nodeInterface;
                parameter.Index = 2;
                parameters.Add(parameter);
                // node type parameter (current pointer of caller)
                ParameterDefinition currentNodeCallerParameter = new ParameterDefinition();
                currentNodeCallerParameter.IsIn = false;
                currentNodeCallerParameter.IsOptional = false;
                currentNodeCallerParameter.IsOut = false;
                currentNodeCallerParameter.Type = this.nodeInterface;
                currentNodeCallerParameter.Index = 3;
                parameters.Add(currentNodeCallerParameter);

                MethodDefinition dumpGraphRecMethod = this.helperClass.createNewMethod("DEBUG_dumpGraphRec", this.targetClass, host.PlatformType.SystemVoid, TypeMemberVisibility.Public, parameters, CallingConvention.HasThis, false, false, true);
                currentNodeCallerParameter.ContainingSignature = dumpGraphRecMethod; // is needed when parameter is accessed via Ldarg

                // create body for dumpGraph method
                ilGenerator = new ILGenerator(host, this.debuggingDumpGraphMethod);
                localVariables = new List<ILocalDefinition>();

                // create local string variable needed for debugging code
                LocalDefinition nodeNameLocal = new LocalDefinition();
                nodeNameLocal.IsReference = false;
                nodeNameLocal.IsPinned = false;
                nodeNameLocal.IsModified = false;
                nodeNameLocal.Type = this.host.PlatformType.SystemString;
                nodeNameLocal.MethodDefinition = this.debuggingDumpGraphMethod;
                localVariables.Add(nodeNameLocal);

                // create local stream writer variable needed for debugging code
                LocalDefinition streamWriterLocal = new LocalDefinition();
                streamWriterLocal.IsReference = false;
                streamWriterLocal.IsPinned = false;
                streamWriterLocal.IsModified = false;
                streamWriterLocal.Type = this.helperClass.systemIOStreamWriter;
                streamWriterLocal.MethodDefinition = this.debuggingDumpGraphMethod;
                localVariables.Add(streamWriterLocal);

                // create local integer variable for the for loop needed for debugging code
                LocalDefinition forIntegerLocal = new LocalDefinition();
                forIntegerLocal.IsReference = false;
                forIntegerLocal.IsPinned = false;
                forIntegerLocal.IsModified = false;
                forIntegerLocal.Type = this.host.PlatformType.SystemInt32;
                forIntegerLocal.MethodDefinition = this.debuggingDumpGraphMethod;
                localVariables.Add(forIntegerLocal);

                // generate dump file location string
                ilGenerator.Emit(OperationCode.Ldstr, this.debuggingDumpLocation + this.debuggingDumpFilePrefix);
                ilGenerator.Emit(OperationCode.Ldarg_1);
                ilGenerator.Emit(OperationCode.Ldstr, ".dot");
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatThree);

                // initialize io stream writer
                ilGenerator.Emit(OperationCode.Newobj, this.helperClass.streamWriterCtor);
                ilGenerator.Emit(OperationCode.Stloc, streamWriterLocal);

                // initialize .dot file
                ilGenerator.Emit(OperationCode.Ldloc, streamWriterLocal);
                ilGenerator.Emit(OperationCode.Ldstr, "digraph G {");
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWriteLine);

                // check if the node to dump is the same as the current node of the class
                // if it is => color the current dumped node
                ilGenerator.Emit(OperationCode.Ldarg_3);
                ilGenerator.Emit(OperationCode.Ldarg_2);
                ilGenerator.Emit(OperationCode.Ceq);
                ILGeneratorLabel currentNodeEqualDumpedNodeBranch = new ILGeneratorLabel();
                ilGenerator.Emit(OperationCode.Brtrue, currentNodeEqualDumpedNodeBranch);

                // case: current dumped node is not the current node of the class
                // create name for the nodes
                ilGenerator.Emit(OperationCode.Ldstr, "node_0");
                ilGenerator.Emit(OperationCode.Stloc, nodeNameLocal);

                // write current node to .dot file
                ilGenerator.Emit(OperationCode.Ldloc, streamWriterLocal);
                ilGenerator.Emit(OperationCode.Ldloc, nodeNameLocal);
                ilGenerator.Emit(OperationCode.Ldstr, " [shape=record]");
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatTwo);
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWriteLine);

                // jump to the end of this case
                ILGeneratorLabel currentNodeDumpedBranch = new ILGeneratorLabel();
                ilGenerator.Emit(OperationCode.Br, currentNodeDumpedBranch);

                // case: current dumped node is the current node of the class
                ilGenerator.MarkLabel(currentNodeEqualDumpedNodeBranch);

                // create name for the nodes
                ilGenerator.Emit(OperationCode.Ldstr, "node_0");
                ilGenerator.Emit(OperationCode.Stloc, nodeNameLocal);

                // write current node to .dot file
                ilGenerator.Emit(OperationCode.Ldloc, streamWriterLocal);
                ilGenerator.Emit(OperationCode.Ldloc, nodeNameLocal);
                ilGenerator.Emit(OperationCode.Ldstr, " [shape=record, color=blue]");
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatTwo);
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWriteLine);

                // end of the case
                ilGenerator.MarkLabel(currentNodeDumpedBranch);

                // write start of label of the current node to .dot file
                ilGenerator.Emit(OperationCode.Ldloc, streamWriterLocal);
                ilGenerator.Emit(OperationCode.Ldloc, nodeNameLocal);
                ilGenerator.Emit(OperationCode.Ldstr, " [label=\"{");
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatTwo);
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWrite);

                // write current node name to .dot file
                ilGenerator.Emit(OperationCode.Ldloc, streamWriterLocal);
                ilGenerator.Emit(OperationCode.Ldstr, "Node: ");
                ilGenerator.Emit(OperationCode.Ldloc, nodeNameLocal);
                ilGenerator.Emit(OperationCode.Ldstr, "|");
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatThree);
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWrite);

                // write current node id to .dot file
                ilGenerator.Emit(OperationCode.Ldloc, streamWriterLocal);
                ilGenerator.Emit(OperationCode.Ldstr, "Id: ");
                ilGenerator.Emit(OperationCode.Ldarg_2);
                ilGenerator.Emit(OperationCode.Callvirt, this.debuggingInterfaceIdGet);
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatTwo);
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWrite);

                // write end of label of the current node to .dot file
                ilGenerator.Emit(OperationCode.Ldloc, streamWriterLocal);
                ilGenerator.Emit(OperationCode.Ldstr, "}\"]");
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWriteLine);

                // initialize counter of for loop
                ilGenerator.Emit(OperationCode.Ldc_I4_0);
                ilGenerator.Emit(OperationCode.Stloc, forIntegerLocal);

                // jump to loop condition
                loopConditionBranch = new ILGeneratorLabel();
                loopStartBranch = new ILGeneratorLabel();
                ilGenerator.Emit(OperationCode.Br, loopConditionBranch);

                // start of loop
                ilGenerator.MarkLabel(loopStartBranch);

                // check if childNodes[i] == startNode
                ilGenerator.Emit(OperationCode.Ldarg_2);
                ilGenerator.Emit(OperationCode.Ldloc, forIntegerLocal);
                ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);
                ilGenerator.Emit(OperationCode.Ldarg_0);
                ilGenerator.Emit(OperationCode.Callvirt, this.interfaceStartPointerGet);
                ilGenerator.Emit(OperationCode.Ceq);
                loopConditionAndIncBranch = new ILGeneratorLabel();
                ilGenerator.Emit(OperationCode.Brtrue, loopConditionAndIncBranch);

                // write connection of current node to next node to .dot file
                ilGenerator.Emit(OperationCode.Ldloc, streamWriterLocal);

                // generate first part of the string
                ilGenerator.Emit(OperationCode.Ldloc, nodeNameLocal);
                ilGenerator.Emit(OperationCode.Ldstr, " -> ");
                ilGenerator.Emit(OperationCode.Ldloc, nodeNameLocal);
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatThree);

                // generate second part of string and concat to first part
                ilGenerator.Emit(OperationCode.Ldstr, "_");
                ilGenerator.Emit(OperationCode.Ldloca, forIntegerLocal);
                ilGenerator.Emit(OperationCode.Call, this.helperClass.int32ToString);
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatThree);

                // write to .dot file
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWriteLine);

                // call method that dumps graph recursively ( this.dumpGraphRec(streamWriter, String name, nextNode) )
                ilGenerator.Emit(OperationCode.Ldarg_0);

                // push stream writer parameter
                ilGenerator.Emit(OperationCode.Ldloc, streamWriterLocal);

                // push string parameter
                ilGenerator.Emit(OperationCode.Ldloc, nodeNameLocal);
                ilGenerator.Emit(OperationCode.Ldstr, "_");
                ilGenerator.Emit(OperationCode.Ldloca, forIntegerLocal);
                ilGenerator.Emit(OperationCode.Call, this.helperClass.int32ToString);
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatThree);

                // push node parameter (current pointer of debug method)
                ilGenerator.Emit(OperationCode.Ldarg_2);
                ilGenerator.Emit(OperationCode.Ldloc, forIntegerLocal);
                ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);

                // push node parameter (current pointer of the caller)
                ilGenerator.Emit(OperationCode.Ldarg_3);

                ilGenerator.Emit(OperationCode.Callvirt, dumpGraphRecMethod);

                // increment loop counter
                ilGenerator.MarkLabel(loopConditionAndIncBranch);
                ilGenerator.Emit(OperationCode.Ldloc, forIntegerLocal);
                ilGenerator.Emit(OperationCode.Ldc_I4_1);
                ilGenerator.Emit(OperationCode.Add);
                ilGenerator.Emit(OperationCode.Stloc, forIntegerLocal);

                // loop condition
                ilGenerator.MarkLabel(loopConditionBranch);
                ilGenerator.Emit(OperationCode.Ldloc, forIntegerLocal);
                ilGenerator.Emit(OperationCode.Ldc_I4, this.graphDimension);
                ilGenerator.Emit(OperationCode.Clt);
                ilGenerator.Emit(OperationCode.Brtrue, loopStartBranch);

                // end .dot file
                ilGenerator.Emit(OperationCode.Ldloc, streamWriterLocal);
                ilGenerator.Emit(OperationCode.Ldstr, "}");
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWriteLine);

                // close io stream writer                
                ilGenerator.Emit(OperationCode.Ldloc, streamWriterLocal);
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterClose);

                ilGenerator.Emit(OperationCode.Ret);

                // create body
                body = new ILGeneratorMethodBody(ilGenerator, true, 8, this.debuggingDumpGraphMethod, localVariables, Enumerable<ITypeDefinition>.Empty);
                this.debuggingDumpGraphMethod.Body = body;

                // create body for dumpGraphRec method
                localVariables = new List<ILocalDefinition>();
                ilGenerator = new ILGenerator(host, dumpGraphRecMethod);

                // create local integer variable for the for loop needed for debugging code
                forIntegerLocal = new LocalDefinition();
                forIntegerLocal.IsReference = false;
                forIntegerLocal.IsPinned = false;
                forIntegerLocal.IsModified = false;
                forIntegerLocal.Type = this.host.PlatformType.SystemInt32;
                forIntegerLocal.MethodDefinition = dumpGraphRecMethod;
                localVariables.Add(forIntegerLocal);

                // check if the node to dump is the same as the current node of the class
                // if it is => color the current dumped node
                ilGenerator.Emit(OperationCode.Ldarg, currentNodeCallerParameter);
                ilGenerator.Emit(OperationCode.Ldarg_3);
                ilGenerator.Emit(OperationCode.Ceq);
                currentNodeEqualDumpedNodeBranch = new ILGeneratorLabel();
                ilGenerator.Emit(OperationCode.Brtrue, currentNodeEqualDumpedNodeBranch);

                // case: current dumped node is not the current node of the class
                // write current node to .dot file
                ilGenerator.Emit(OperationCode.Ldarg_1);
                ilGenerator.Emit(OperationCode.Ldarg_2);
                ilGenerator.Emit(OperationCode.Ldstr, " [shape=record]");
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatTwo);
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWriteLine);

                // jump to the end of this case
                currentNodeDumpedBranch = new ILGeneratorLabel();
                ilGenerator.Emit(OperationCode.Br, currentNodeDumpedBranch);

                // case: current dumped node is the current node of the class
                ilGenerator.MarkLabel(currentNodeEqualDumpedNodeBranch);

                // write current node to .dot file
                ilGenerator.Emit(OperationCode.Ldarg_1);
                ilGenerator.Emit(OperationCode.Ldarg_2);
                ilGenerator.Emit(OperationCode.Ldstr, " [shape=record, color=blue]");
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatTwo);
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWriteLine);

                // end of the case
                ilGenerator.MarkLabel(currentNodeDumpedBranch);

                // write start of label of the current node to .dot file
                ilGenerator.Emit(OperationCode.Ldarg_1);
                ilGenerator.Emit(OperationCode.Ldarg_2);
                ilGenerator.Emit(OperationCode.Ldstr, " [label=\"{");
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatTwo);
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWrite);

                // write current node name to .dot file
                ilGenerator.Emit(OperationCode.Ldarg_1);
                ilGenerator.Emit(OperationCode.Ldstr, "Node: ");
                ilGenerator.Emit(OperationCode.Ldarg_2);
                ilGenerator.Emit(OperationCode.Ldstr, "|");
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatThree);
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWrite);

                // write current node id to .dot file
                ilGenerator.Emit(OperationCode.Ldarg_1);
                ilGenerator.Emit(OperationCode.Ldstr, "Id: ");
                ilGenerator.Emit(OperationCode.Ldarg_3);
                ilGenerator.Emit(OperationCode.Callvirt, this.debuggingInterfaceIdGet);
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatTwo);
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWrite);

                // write end of label of the current node to .dot file
                ilGenerator.Emit(OperationCode.Ldarg_1);
                ilGenerator.Emit(OperationCode.Ldstr, "}\"]");
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWriteLine);

                // initialize counter of for loop
                ilGenerator.Emit(OperationCode.Ldc_I4_0);
                ilGenerator.Emit(OperationCode.Stloc, forIntegerLocal);

                // jump to loop condition
                loopConditionBranch = new ILGeneratorLabel();
                loopStartBranch = new ILGeneratorLabel();
                ilGenerator.Emit(OperationCode.Br, loopConditionBranch);

                // start of loop
                ilGenerator.MarkLabel(loopStartBranch);

                // check if childNodes[i] == startNode
                ilGenerator.Emit(OperationCode.Ldarg_3);
                ilGenerator.Emit(OperationCode.Ldloc, forIntegerLocal);
                ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);
                ilGenerator.Emit(OperationCode.Ldarg_0);
                ilGenerator.Emit(OperationCode.Callvirt, this.interfaceStartPointerGet);
                ilGenerator.Emit(OperationCode.Ceq);
                loopConditionAndIncBranch = new ILGeneratorLabel();
                ilGenerator.Emit(OperationCode.Brtrue, loopConditionAndIncBranch);

                // write connection of current node to next node to .dot file
                ilGenerator.Emit(OperationCode.Ldarg_1);

                // generate first part of the string
                ilGenerator.Emit(OperationCode.Ldarg_2);
                ilGenerator.Emit(OperationCode.Ldstr, " -> ");
                ilGenerator.Emit(OperationCode.Ldarg_2);
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatThree);

                // generate second part of string and concat to first part
                ilGenerator.Emit(OperationCode.Ldstr, "_");
                ilGenerator.Emit(OperationCode.Ldloca, forIntegerLocal);
                ilGenerator.Emit(OperationCode.Call, this.helperClass.int32ToString);
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatThree);

                // write to .dot file
                ilGenerator.Emit(OperationCode.Callvirt, this.helperClass.textWriterWriteLine);

                // call method that dumps graph recursively ( this.dumpGraphRec(streamWriter, String name, nextNode) )
                ilGenerator.Emit(OperationCode.Ldarg_0);

                // push stream writer parameter
                ilGenerator.Emit(OperationCode.Ldarg_1);

                // push string parameter
                ilGenerator.Emit(OperationCode.Ldarg_2);
                ilGenerator.Emit(OperationCode.Ldstr, "_");
                ilGenerator.Emit(OperationCode.Ldloca, forIntegerLocal);
                ilGenerator.Emit(OperationCode.Call, this.helperClass.int32ToString);
                ilGenerator.Emit(OperationCode.Call, this.helperClass.stringConcatThree);

                // push node parameter (current pointer of debug method)
                ilGenerator.Emit(OperationCode.Ldarg_3);
                ilGenerator.Emit(OperationCode.Ldloc, forIntegerLocal);
                ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);

                // push node parameter (current pointer of the caller)
                ilGenerator.Emit(OperationCode.Ldarg, currentNodeCallerParameter);

                ilGenerator.Emit(OperationCode.Callvirt, dumpGraphRecMethod);

                // increment loop counter
                ilGenerator.MarkLabel(loopConditionAndIncBranch);
                ilGenerator.Emit(OperationCode.Ldloc, forIntegerLocal);
                ilGenerator.Emit(OperationCode.Ldc_I4_1);
                ilGenerator.Emit(OperationCode.Add);
                ilGenerator.Emit(OperationCode.Stloc, forIntegerLocal);

                // loop condition
                ilGenerator.MarkLabel(loopConditionBranch);
                ilGenerator.Emit(OperationCode.Ldloc, forIntegerLocal);
                ilGenerator.Emit(OperationCode.Ldc_I4, this.graphDimension);
                ilGenerator.Emit(OperationCode.Clt);
                ilGenerator.Emit(OperationCode.Brtrue, loopStartBranch);

                ilGenerator.Emit(OperationCode.Ret);

                // create body
                body = new ILGeneratorMethodBody(ilGenerator, true, 8, dumpGraphRecMethod, localVariables, Enumerable<ITypeDefinition>.Empty);
                dumpGraphRecMethod.Body = body;

            }

            // inject code to build the graph to all constructors (except the artificial added ctor for a graph node)
            foreach (MethodDefinition ctorMethod in this.targetClass.Methods) {

                // only process constructors
                if (!ctorMethod.IsConstructor) {
                    continue;
                }

                // skip the artificial added ctor with the node interface as parameter
                bool skip = false;
                if (ctorMethod.Parameters != null) {
                    foreach (IParameterDefinition ctorParameter in ctorMethod.Parameters) {
                        if (ctorParameter.Type == this.nodeInterface) {
                            skip = true;
                            break;
                        }
                    }
                }
                if (skip) {
                    continue;
                }

                this.logger.writeLine("Injecting code to build graph to \"" + this.logger.makeFuncSigString(ctorMethod) + "\"");

                MethodCfg ctorMethodCfg = this.cfgBuilder.buildCfgForMethod(ctorMethod);

                // create new basic block that builds the graph
                // (will be the new starting basic block of the method)
                BasicBlock startBasicBlock = new BasicBlock();
                startBasicBlock.startIdx = 0;
                startBasicBlock.endIdx = 0;

                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.buildGraphMethod));

                if (this.debugging) {

                    // dump generated graph
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, this.debuggingNumber.ToString().PadLeft(3, '0') + "_obfu_" + this.logger.makeFuncSigString(ctorMethodCfg.method)));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.interfaceStartPointerGet));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.interfaceStartPointerGet));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.debuggingDumpGraphMethod));

                }

                // create exit branch for the new start basic block
                NoBranchTarget startExitBranch = new NoBranchTarget();
                startExitBranch.sourceBasicBlock = startBasicBlock;
                startBasicBlock.exitBranch = startExitBranch;

                // set the original start basic block as the target of the exit branch
                startExitBranch.takenTarget = ctorMethodCfg.startBasicBlock;
                ctorMethodCfg.startBasicBlock.entryBranches.Add(startExitBranch);

                // set new start basic block as start basic block of the method cfg
                ctorMethodCfg.startBasicBlock = startBasicBlock;
                ctorMethodCfg.basicBlocks.Add(startBasicBlock);

                this.cfgBuilder.createMethodFromCfg(ctorMethodCfg);

            }

        }


        // this function adds recursively methods that adds the left and the right node to the current node in the graph
        // (and calls the next method that adds the left and right node)
        private MethodDefinition addNodeRecursively(NodeObject currentNode, int currentDepth, String newMethodName) {

            // create first parameter that is the current node in the graph
            List<IParameterDefinition> parameters = new List<IParameterDefinition>();
            ParameterDefinition parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Type = this.nodeInterface;
            parameters.Add(parameter);

            // create second parameter that is the current depth in the graph
            parameter = new ParameterDefinition();
            parameter.IsIn = false;
            parameter.IsOptional = false;
            parameter.IsOut = false;
            parameter.Type = host.PlatformType.SystemInt32;
            parameters.Add(parameter);

            // create method to add left and right node to the graph
            MethodDefinition newAddNodeMethod = this.helperClass.createNewMethod(newMethodName, this.targetClass, host.PlatformType.SystemVoid, TypeMemberVisibility.Public, parameters, CallingConvention.HasThis, false, false, true); // TODO: method name

            // create body of method
            ILGenerator ilGenerator = new ILGenerator(this.host, newAddNodeMethod);

            // check if max depth of tree is reached
            ilGenerator.Emit(OperationCode.Ldarg_2);
            ilGenerator.Emit(OperationCode.Ldc_I4, this.graph.graphDepth);
            ilGenerator.Emit(OperationCode.Ceq);
            ilGenerator.Emit(OperationCode.Ldc_I4_0);
            ilGenerator.Emit(OperationCode.Ceq);
            ILGeneratorLabel depthReachedBranch = new ILGeneratorLabel();
            ilGenerator.Emit(OperationCode.Brfalse, depthReachedBranch);

            // set child nodes of this node
            MethodDefinition constructorToUse;

            for (int i = 0; i < this.graphDimension; i++) {

                // get constructor of the child node element
                constructorToUse = null;

                // check if the child node exists in the graph and the max depth is not yet reached
                if (currentNode.nodeObjects[i] == null
                    && currentDepth != this.graph.graphDepth) {
                    throw new ArgumentException("Given depth of graph is larger than the actual graph.");
                }
                else {
                    // check if the child node does not exist
                    // => use a random constructor node
                    if (currentNode.nodeObjects[i] == null) {
                        constructorToUse = currentNode.constructorToUse; // TODO: use random node constructor here
                    }
                    // else use constructor of given graph node
                    else {
                        constructorToUse = currentNode.nodeObjects[i].constructorToUse;
                    }
                }

                // set child node
                ilGenerator.Emit(OperationCode.Ldarg_1); // current node from which the function is called
                ilGenerator.Emit(OperationCode.Ldarg_0); // needed as argument for the constructor
                ilGenerator.Emit(OperationCode.Newobj, constructorToUse); // iNode parameter
                ilGenerator.Emit(OperationCode.Ldc_I4, i); // index parameter
                ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesSet);

                // get child node and set it recursively
                ilGenerator.Emit(OperationCode.Ldarg_0);
                ilGenerator.Emit(OperationCode.Ldarg_1); // current node from which the function is called
                ilGenerator.Emit(OperationCode.Ldc_I4, i); // index parameter
                ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesGet);

                ilGenerator.Emit(OperationCode.Ldarg_2); // arg2 (current depth) + 1
                ilGenerator.Emit(OperationCode.Ldc_I4_1);
                ilGenerator.Emit(OperationCode.Add);

                MethodDefinition newMethodToCall = null;
                if (currentDepth == this.graph.graphDepth) {
                    newMethodToCall = newAddNodeMethod; // TODO: call random node generation method here
                }
                else {
                    newMethodToCall = this.addNodeRecursively(currentNode.nodeObjects[i], currentDepth + 1, newMethodName + "_" + i.ToString()); // TODO: method name
                }

                ilGenerator.Emit(OperationCode.Callvirt, newMethodToCall);
            }

            ilGenerator.Emit(OperationCode.Ret);

            // set child nodes of this node to null
            ilGenerator.MarkLabel(depthReachedBranch);
            for (int i = 0; i < this.graphDimension; i++) {
                ilGenerator.Emit(OperationCode.Ldarg_1);
                ilGenerator.Emit(OperationCode.Ldarg_0);
                ilGenerator.Emit(OperationCode.Callvirt, this.interfaceStartPointerGet); // iNode parameter
                ilGenerator.Emit(OperationCode.Ldc_I4, i); // index parameter
                ilGenerator.Emit(OperationCode.Callvirt, this.interfaceChildNodesSet);
            }
            ilGenerator.Emit(OperationCode.Ret);

            // generate body
            IMethodBody body = new ILGeneratorMethodBody(ilGenerator, true, 8, newAddNodeMethod, Enumerable<ILocalDefinition>.Empty, Enumerable<ITypeDefinition>.Empty);
            newAddNodeMethod.Body = body;

            return newAddNodeMethod;
        }


        // this function generates code for an opaque predicate that evaluates to "false"
        // => the "not taken" branch of the conditional branch have to be taken for the correct code path
        private void createCodeFalsePredicate(ref BasicBlock outStartBasicBlock, ref ConditionalBranchTarget outConditionalBranch, BasicBlockGraphNodeLink link, LocalDefinition currentNodeLocal) {

            // create a basic block that checks the opaque predicate (this check evaluates to "false")
            BasicBlock startBasicBlock = new BasicBlock();
            startBasicBlock.startIdx = 0;
            startBasicBlock.endIdx = 0;

            // add graph transformer metadata to the basic block
            GraphTransformerPredicateBasicBlock metadata = new GraphTransformerPredicateBasicBlock();
            metadata.predicateType = GraphOpaquePredicate.False;
            metadata.correspondingGraphNodes.Add(link);
            startBasicBlock.transformationMetadata.Add(metadata);

            // search for the valid and invalid interfaces list of the current valid path id
            List<ITypeReference> validInterfaces = null;
            List<ITypeReference> invalidInterfaces = null;
            foreach (PathElement pathElement in link.graphNode.pathElements) {
                if (pathElement.validPathId == link.validPathId) {
                    validInterfaces = pathElement.validInterfaces;
                    invalidInterfaces = pathElement.invalidInterfaces;
                }
            }
            if(validInterfaces == null
                || invalidInterfaces == null) {
                    throw new ArgumentException("No valid/invalid interface list was found.");
            }

            // add a random check to the code
            // important: for this predicate the dead code is reached by the "taken" branch and the correct code is reached by the "not taken" branch
            int randPosition;
            ITypeReference interfaceToCheck = null;
            switch (this.prng.Next(8)) {
                case 0:
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                    randPosition = this.prng.Next(validInterfaces.Count());
                    interfaceToCheck = validInterfaces.ElementAt(randPosition);
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Cgt_Un));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brfalse, 0));
                    break;

                case 1:
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                    randPosition = this.prng.Next(validInterfaces.Count());
                    interfaceToCheck = validInterfaces.ElementAt(randPosition);
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Cgt_Un));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldc_I4_0));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ceq));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brtrue, 0));
                    break;

                case 2:
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                    randPosition = this.prng.Next(validInterfaces.Count());
                    interfaceToCheck = validInterfaces.ElementAt(randPosition);
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Cgt_Un));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldc_I4_1));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ceq));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brfalse, 0));
                    break;

                case 3:
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                    randPosition = this.prng.Next(validInterfaces.Count());
                    interfaceToCheck = validInterfaces.ElementAt(randPosition);
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ceq));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brtrue, 0));
                    break;

                case 4:
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                    randPosition = this.prng.Next(invalidInterfaces.Count());
                    interfaceToCheck = invalidInterfaces.ElementAt(randPosition);
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Cgt_Un));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brtrue, 0));
                    break;

                case 5:
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                    randPosition = this.prng.Next(invalidInterfaces.Count());
                    interfaceToCheck = invalidInterfaces.ElementAt(randPosition);
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Cgt_Un));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldc_I4_0));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ceq));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brfalse, 0));
                    break;

                case 6:
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                    randPosition = this.prng.Next(invalidInterfaces.Count());
                    interfaceToCheck = invalidInterfaces.ElementAt(randPosition);
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Cgt_Un));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldc_I4_1));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ceq));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brtrue, 0));
                    break;

                case 7:
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                    randPosition = this.prng.Next(invalidInterfaces.Count());
                    interfaceToCheck = invalidInterfaces.ElementAt(randPosition);
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ceq));
                    startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brfalse, 0));
                    break;

                default:
                    throw new ArgumentException("This case should never be reached.");
            }

            // create the conditional branch for the checking basic block (not taken => correct code; taken => dead code)
            ConditionalBranchTarget startBasicBlockExitBranch = new ConditionalBranchTarget();
            startBasicBlockExitBranch.sourceBasicBlock = startBasicBlock;
            startBasicBlock.exitBranch = startBasicBlockExitBranch;


            // create the dead code for this opaque predicate
            BasicBlock deadCodeBasicBlock = new BasicBlock();
            deadCodeBasicBlock.startIdx = 0;
            deadCodeBasicBlock.endIdx = 0;

            // set the dead code basic block as "taken" target for the conditional branch of the checking basic block
            deadCodeBasicBlock.entryBranches.Add(startBasicBlockExitBranch);
            startBasicBlockExitBranch.takenTarget = deadCodeBasicBlock;

            // generate dead code for the dead code basic block
            GraphTransformerDeadCodeBasicBlock deadCodeMetadata = new GraphTransformerDeadCodeBasicBlock();
            deadCodeBasicBlock.transformationMetadata.Add(deadCodeMetadata);

            // set the created basic block and conditional jumps as output
            outStartBasicBlock = startBasicBlock;
            outConditionalBranch = startBasicBlockExitBranch;

        }


        // Generates code for an opaque predicate evaluating to a random value.
        // => Both branches point to basic blocks with equal semantics.
        private void createCodeRandomPredicate(ref BasicBlock outStartBasicBlock, ref ConditionalBranchTarget outConditionalBranch,
            BasicBlockGraphNodeLink link, LocalDefinition currentNodeLocal)
        {
            throw new ArgumentException("Not implemented."); // TODO: will improve the probabilistic control flow
        }


        // this function generates code for an opaque predicate that evaluates to "true"
        // => the "taken" branch of the conditional branch have to be taken for the correct code path
        private void createCodeTruePredicate(ref BasicBlock outStartBasicBlock, ref ConditionalBranchTarget outConditionalBranch,
            BasicBlockGraphNodeLink link, LocalDefinition currentNodeLocal) {

            // create a basic block that checks the opaque predicate (this check evaluates to "true")
            BasicBlock startBasicBlock = new BasicBlock();
            startBasicBlock.startIdx = 0;
            startBasicBlock.endIdx = 0;

            // add graph transformer metadata to the basic block
            GraphTransformerPredicateBasicBlock metadata = new GraphTransformerPredicateBasicBlock();
            metadata.predicateType = GraphOpaquePredicate.True;
            metadata.correspondingGraphNodes.Add(link);
            startBasicBlock.transformationMetadata.Add(metadata);

            // search for the valid and invalid interfaces list of the current valid path id
            List<ITypeReference> validInterfaces = null;
            List<ITypeReference> invalidInterfaces = null;
            foreach (PathElement pathElement in link.graphNode.pathElements) {
                if (pathElement.validPathId == link.validPathId) {
                    validInterfaces = pathElement.validInterfaces;
                    invalidInterfaces = pathElement.invalidInterfaces;
                }
            }
            if (validInterfaces == null
                || invalidInterfaces == null) {
                throw new ArgumentException("No valid/invalid interface list was found.");
            }

            emitPredicateCode(startBasicBlock, currentNodeLocal, validInterfaces, invalidInterfaces);

            // create the conditional branch for the checking basic block (taken => correct code; not taken => dead code)
            ConditionalBranchTarget startBasicBlockExitBranch = new ConditionalBranchTarget();
            startBasicBlockExitBranch.sourceBasicBlock = startBasicBlock;
            startBasicBlock.exitBranch = startBasicBlockExitBranch;

            // create the dead code for this opaque predicate
            BasicBlock deadCodeBasicBlock = new BasicBlock();
            deadCodeBasicBlock.startIdx = 0;
            deadCodeBasicBlock.endIdx = 0;

            // set the dead code basic block as "not taken" target for the conditional branch of the checking basic block
            deadCodeBasicBlock.entryBranches.Add(startBasicBlockExitBranch);
            startBasicBlockExitBranch.notTakenTarget = deadCodeBasicBlock;

            // generate dead code for the dead code basic block
            GraphTransformerDeadCodeBasicBlock deadCodeMetadata = new GraphTransformerDeadCodeBasicBlock();
            deadCodeBasicBlock.transformationMetadata.Add(deadCodeMetadata);

            // set the created basic block and conditional jumps as output
            outStartBasicBlock = startBasicBlock;
            outConditionalBranch = startBasicBlockExitBranch;

        }


        private void emitPredicateCode(BasicBlock startBasicBlock, LocalDefinition currentNodeLocal,
            List<ITypeReference> validInterfaces, List<ITypeReference> invalidInterfaces)
        {
            // add a random check to the code
            // important: for this predicate the dead code is reached by the "not taken" branch and the correct code is reached by the "taken" branch
            int randPosition;
            ITypeReference interfaceToCheck = null;
            switch(this.prng.Next(8)) {
            case 0:
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                randPosition = this.prng.Next(validInterfaces.Count());
                interfaceToCheck = validInterfaces.ElementAt(randPosition);
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Cgt_Un));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brtrue, 0));
                break;

            case 1:
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                randPosition = this.prng.Next(validInterfaces.Count());
                interfaceToCheck = validInterfaces.ElementAt(randPosition);
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Cgt_Un));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldc_I4_0));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ceq));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brfalse, 0));
                break;

            case 2:
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                randPosition = this.prng.Next(validInterfaces.Count());
                interfaceToCheck = validInterfaces.ElementAt(randPosition);
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Cgt_Un));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldc_I4_1));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ceq));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brtrue, 0));
                break;

            case 3:
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                randPosition = this.prng.Next(validInterfaces.Count());
                interfaceToCheck = validInterfaces.ElementAt(randPosition);
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ceq));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brfalse, 0));
                break;

            case 4:
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                randPosition = this.prng.Next(invalidInterfaces.Count());
                interfaceToCheck = invalidInterfaces.ElementAt(randPosition);
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Cgt_Un));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brfalse, 0));
                break;

            case 5:

                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                randPosition = this.prng.Next(invalidInterfaces.Count());
                interfaceToCheck = invalidInterfaces.ElementAt(randPosition);
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Cgt_Un));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldc_I4_0));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ceq));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brtrue, 0));
                break;

            case 6:
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                randPosition = this.prng.Next(invalidInterfaces.Count());
                interfaceToCheck = invalidInterfaces.ElementAt(randPosition);
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Cgt_Un));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldc_I4_1));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ceq));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brfalse, 0));
                break;

            case 7:
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));

                randPosition = this.prng.Next(invalidInterfaces.Count());
                interfaceToCheck = invalidInterfaces.ElementAt(randPosition);
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Isinst, interfaceToCheck));

                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldnull));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ceq));
                startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Brtrue, 0));
                break;

            default:
                throw new ArgumentException("This case should never be reached.");
            }
        }


        // this function generates code to move the current pointer to the next child node (or start node if there is no next child node)
        private BasicBlock createCodeNextNode(IBranchTarget usedExitBranch, BasicBlockGraphNodeLink graphLink, LocalDefinition currentNodeLocal, int index, bool correctNextNode) {

            // create graph transformer metadata for the basic blocks
            GraphTransformerNextNodeBasicBlock metadata = new GraphTransformerNextNodeBasicBlock();
            metadata.correctNextNode = correctNextNode;
            metadata.correspondingGraphNodes.Add(graphLink);
            
            // create new basic block
            BasicBlock nextNodeBasicBlock = new BasicBlock();
            nextNodeBasicBlock.startIdx = 0;
            nextNodeBasicBlock.endIdx = 0;
            nextNodeBasicBlock.transformationMetadata.Add(metadata);

            nextNodeBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));
            nextNodeBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldc_I4, index)); // TODO perhaps a more dynamically calculated index?
            nextNodeBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.interfaceChildNodesGet));
            nextNodeBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Stloc, currentNodeLocal));

            // check which exit branch is used to inject the code to the cfg and generate code for it accordingly
            if ((usedExitBranch as NoBranchTarget) != null) {
                nextNodeBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Nop));
            }
            else if ((usedExitBranch as UnconditionalBranchTarget) != null) {
                nextNodeBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Br));
            }
            else {
                throw new ArgumentException("Do not know how to generate code for given exit branch.");
            }

            // add exit branch to newly created basic block
            nextNodeBasicBlock.exitBranch = usedExitBranch;
            usedExitBranch.sourceBasicBlock = nextNodeBasicBlock;

            return nextNodeBasicBlock;

        }


        // this function generates code for the "state switch"
        private void createCodeStateSwitch(ref BasicBlock outStateBasicBlock, ref SwitchBranchTarget outStateExitBranch, LocalDefinition intStateLocal, List<BasicBlockGraphNodeLink> links) {

            BasicBlock stateBasicBlock = new BasicBlock();
            stateBasicBlock.startIdx = 0;
            stateBasicBlock.endIdx = 0;

            // create graph transformer metadata for the basic block
            GraphTransformerStateBasicBlock stateMetadata = new GraphTransformerStateBasicBlock();
            foreach (BasicBlockGraphNodeLink link in links) {
                stateMetadata.correspondingGraphNodes.Add(link);
            }            
            stateBasicBlock.transformationMetadata.Add(stateMetadata);

            // create switch statement for the "state switch"
            stateBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, intStateLocal));
            uint[] switchSize = new uint[this.graph.graphValidPathCount];
            stateBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Switch, switchSize));

            // create exit branch for the new state basic block
            SwitchBranchTarget stateExitBranch = new SwitchBranchTarget();
            stateExitBranch.sourceBasicBlock = stateBasicBlock;
            stateBasicBlock.exitBranch = stateExitBranch;

            // create the dead code for this "state switch"
            BasicBlock deadCodeBasicBlock = new BasicBlock();
            deadCodeBasicBlock.startIdx = 0;
            deadCodeBasicBlock.endIdx = 0;

            // set the dead code basic block as "not taken" target for the conditional branch of the checking basic block
            deadCodeBasicBlock.entryBranches.Add(stateExitBranch);
            stateExitBranch.notTakenTarget = deadCodeBasicBlock; // TODO at the moment always the not taken branch of the switch statement is the dead code basic block

            // generate dead code for the dead code basic block
            GraphTransformerDeadCodeBasicBlock deadCodeMetadata = new GraphTransformerDeadCodeBasicBlock();
            deadCodeBasicBlock.transformationMetadata.Add(deadCodeMetadata);

            // set new created basic blocks and exit branch as output
            outStateBasicBlock = stateBasicBlock;
            outStateExitBranch = stateExitBranch;
        }


        // this function generates code for the "state change"
        private void createCodeStateChange(ref BasicBlock outStateChangeBasicBlock, ref SwitchBranchTarget outStateChangeExitBranch, GraphRandomStateGenerator randomStateGenerator, BasicBlockGraphNodeLink link, LocalDefinition intStateLocal) {

            // create a basic block to change the current global state
            BasicBlock stateChangeBasicBlock = new BasicBlock();
            stateChangeBasicBlock.startIdx = 0;
            stateChangeBasicBlock.endIdx = 0;

            // create graph transformer metadata for the basic block
            GraphTransformerStateChangeBasicBlock metadataChange = new GraphTransformerStateChangeBasicBlock();
            metadataChange.correspondingGraphNodes.Add(link);
            stateChangeBasicBlock.transformationMetadata.Add(metadataChange);

            // generate code for setting the new global state
            List<IOperation> tempOperations = randomStateGenerator.generateCodeSetRandomState(intStateLocal);
            stateChangeBasicBlock.operations.AddRange(tempOperations);

            // create switch statement for the "state switch"
            stateChangeBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, intStateLocal));
            uint[] switchSize = new uint[this.graph.graphValidPathCount];
            stateChangeBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Switch, switchSize));

            // create exit branch for the new state basic block
            SwitchBranchTarget stateChangeExitBranch = new SwitchBranchTarget();
            stateChangeExitBranch.sourceBasicBlock = stateChangeBasicBlock;
            stateChangeBasicBlock.exitBranch = stateChangeExitBranch;

            // create the dead code for this "state switch"
            BasicBlock deadCodeBasicBlock = new BasicBlock();
            deadCodeBasicBlock.startIdx = 0;
            deadCodeBasicBlock.endIdx = 0;

            // set the dead code basic block as "not taken" target for the conditional branch of the checking basic block
            deadCodeBasicBlock.entryBranches.Add(stateChangeExitBranch);
            stateChangeExitBranch.notTakenTarget = deadCodeBasicBlock; // TODO at the moment always the not taken branch of the switch statement is the dead code basic block

            // generate dead code for the dead code basic block
            GraphTransformerDeadCodeBasicBlock deadCodeMetadata = new GraphTransformerDeadCodeBasicBlock();
            deadCodeBasicBlock.transformationMetadata.Add(deadCodeMetadata);

            outStateChangeBasicBlock = stateChangeBasicBlock;
            outStateChangeExitBranch = stateChangeExitBranch;

        }


        // this function generates and injects code into the given entry branch that moves the pointer from the source node to the target node
        private void injectMovePointerCode(CfgManipulator cfgManipulator, NodeObject sourceNode, BasicBlockGraphNodeLink targetLink, IBranchTarget entryBranch, LocalDefinition currentNodeLocal) {

            NodeObject targetNode = targetLink.graphNode;
            NodeObject currentNode = sourceNode;
            UnconditionalBranchTarget branchToManipulate = null;

            if ((entryBranch as UnconditionalBranchTarget) != null) {
                branchToManipulate = (entryBranch as UnconditionalBranchTarget);
            }
            else {
                throw new ArgumentException("Not yet implemented."); // TODO at the moment we assume unconditional branches as entry
            }

            // do nothing if the source node is already the same as the target node
            if (sourceNode == targetNode) {
                return;
            }

            // check if the pointer have to be moved to the start pointer before it can be moved to the target element
            bool goToStart = false;
            if (sourceNode.positionInGraph.Count() >= targetNode.positionInGraph.Count()) {
                goToStart = true;
            }
            else {
                for (int idx = 0; idx < sourceNode.positionInGraph.Count(); idx++) {
                    if (sourceNode.positionInGraph.ElementAt(idx) != targetNode.positionInGraph.ElementAt(idx)) {
                        goToStart = true;
                        break;
                    }
                }
            }

            // check how to move the pointer to the target element
            // => first go to the start pointer
            if (goToStart) {

                // generate and inject code that moves the pointer on a random path forward until the start node of the graph is reached
                while (currentNode != this.graph.startingNode) {

                    // generate code to move the pointer to the next node in the graph and inject it
                    int nextNodeIdx = this.prng.Next(this.graphDimension);
                    UnconditionalBranchTarget injectedExitBranch = new UnconditionalBranchTarget();
                    BasicBlock injectedBasicBlock = this.createCodeNextNode(injectedExitBranch, targetLink, currentNodeLocal, nextNodeIdx, true);
                    cfgManipulator.insertBasicBlockBetweenBranch(branchToManipulate, injectedBasicBlock, injectedExitBranch);

                    // set the next branch to manipulate as the newly created exit branch
                    branchToManipulate = injectedExitBranch;

                    // move pointer to the next node in the graph or to the start node if it does not exist
                    if (currentNode.nodeObjects[nextNodeIdx] == null) {
                        currentNode = this.graph.startingNode;
                    }
                    else {
                        currentNode = currentNode.nodeObjects[nextNodeIdx];
                    }
                }

                // generate and inject code that moves the pointer to the correct position of the given graph node
                foreach (int nextNodeIdx in targetNode.positionInGraph) {

                    // generate code to move the pointer to the next node in the graph and inject it
                    UnconditionalBranchTarget injectedExitBranch = new UnconditionalBranchTarget();
                    BasicBlock injectedBasicBlock = this.createCodeNextNode(injectedExitBranch, targetLink, currentNodeLocal, nextNodeIdx, true);
                    cfgManipulator.insertBasicBlockBetweenBranch(branchToManipulate, injectedBasicBlock, injectedExitBranch);

                    // set the next branch to manipulate as the newly created exit branch
                    branchToManipulate = injectedExitBranch;

                    // move pointer to the next node in the graph or to the start node if it does not exist
                    if (currentNode.nodeObjects[nextNodeIdx] == null) {
                        currentNode = this.graph.startingNode;
                    }
                    else {
                        currentNode = currentNode.nodeObjects[nextNodeIdx];
                    }
                }
            }

            // => go on a direct path to the target element
            else {

                // generate and inject code that moves the pointer to the correct position of the given graph node
                for (int idx = sourceNode.positionInGraph.Count(); idx < targetNode.positionInGraph.Count(); idx++) {

                    // generate code to move the pointer to the next node in the graph and inject it
                    UnconditionalBranchTarget injectedExitBranch = new UnconditionalBranchTarget();
                    BasicBlock injectedBasicBlock = this.createCodeNextNode(injectedExitBranch, targetLink, currentNodeLocal, targetNode.positionInGraph.ElementAt(idx), true);
                    cfgManipulator.insertBasicBlockBetweenBranch(branchToManipulate, injectedBasicBlock, injectedExitBranch);

                    // set the next branch to manipulate as the newly created exit branch
                    branchToManipulate = injectedExitBranch;

                    // move pointer to the next node in the graph or to the start node if it does not exist
                    if (currentNode.nodeObjects[targetNode.positionInGraph.ElementAt(idx)] == null) {
                        currentNode = this.graph.startingNode;
                    }
                    else {
                        currentNode = currentNode.nodeObjects[targetNode.positionInGraph.ElementAt(idx)];
                    }
                }

            }

            // check if the final reached node is the same as the target node
            if (currentNode != targetNode) {
                throw new ArgumentException("Current node and target node are not the same.");
            }

        }


        // creates a duplicate of the given source basic block with the branch targets to semantically equivalent basic blocks
        // (not necessary the target basic block of the given source basic block)
        private BasicBlock duplicateBasicBlock(MethodCfg originalMethodCfg, MethodCfg methodCfg, BasicBlock sourceBasicBlock) {

            // search for a semantically equal basic block in the unmodified method CFG
            int searchSementicId = sourceBasicBlock.semanticId;
            BasicBlock copiedOriginalTargetBasicBlock = null;
            if (searchSementicId == -1) {
                throw new ArgumentException("No distinct semantic id given to search for.");
            }
            foreach (BasicBlock searchBasicBlock in originalMethodCfg.basicBlocks) {
                if (searchBasicBlock.semanticId == searchSementicId) {
                    copiedOriginalTargetBasicBlock = searchBasicBlock;
                    break;
                }
            }
            if (copiedOriginalTargetBasicBlock == null) {
                throw new ArgumentException("Not able to find semantically equal basic block in copied CFG.");
            }

            // create a new basic block
            BasicBlock newBasicBlock = new BasicBlock();

            // copy simple values
            newBasicBlock.semanticId = copiedOriginalTargetBasicBlock.semanticId;
            newBasicBlock.startIdx = copiedOriginalTargetBasicBlock.startIdx;
            newBasicBlock.endIdx = copiedOriginalTargetBasicBlock.endIdx;

            // copy all operations of the basic block
            foreach (IOperation operation in copiedOriginalTargetBasicBlock.operations) {
                newBasicBlock.operations.Add(this.helperClass.createNewOperation(operation.OperationCode, operation.Value, operation.Location, operation.Offset));
            }

            if (copiedOriginalTargetBasicBlock.tryBlocks.Count() != 0
                || copiedOriginalTargetBasicBlock.handlerBlocks.Count() != 0) {
                throw new ArgumentException("Not implemented yet.");
            }

            // create an exit branch for the new basic block
            if ((copiedOriginalTargetBasicBlock.exitBranch as NoBranchTarget) != null) {

                //throw new ArgumentException("Not implemented yet."); // TODO: problem here with having 2 no branches before a basic block (but not necessarily because all branches are processed and exchanged)

                NoBranchTarget exitBranch = (copiedOriginalTargetBasicBlock.exitBranch as NoBranchTarget);

                // create a copied exit branch
                NoBranchTarget copiedExitBranch = new NoBranchTarget();
                copiedExitBranch.sourceBasicBlock = newBasicBlock;
                newBasicBlock.exitBranch = copiedExitBranch;

                // search for all semantically equal basic blocks in the modified method CFG (as possible targets)
                searchSementicId = exitBranch.takenTarget.semanticId;
                List<BasicBlock> possibleTargetBasicBlocks = new List<BasicBlock>();
                foreach (BasicBlock searchBasicBlock in methodCfg.basicBlocks) {
                    if (searchBasicBlock.semanticId == searchSementicId) {
                        possibleTargetBasicBlocks.Add(searchBasicBlock);
                    }
                }
                if (possibleTargetBasicBlocks.Count() == 0) {
                    throw new ArgumentException("Not able to find any semantically equal basic block in CFG.");
                }

                // use a random possible basic block as target
                int randPosition = this.prng.Next(possibleTargetBasicBlocks.Count());
                BasicBlock targetBasicBlock = possibleTargetBasicBlocks.ElementAt(randPosition);

                // set basic block as target for the branch
                targetBasicBlock.entryBranches.Add(copiedExitBranch);
                copiedExitBranch.takenTarget = targetBasicBlock;

            }

            else if (copiedOriginalTargetBasicBlock.exitBranch as UnconditionalBranchTarget != null) {
                UnconditionalBranchTarget exitBranch = (copiedOriginalTargetBasicBlock.exitBranch as UnconditionalBranchTarget);

                // create a copied exit branch
                UnconditionalBranchTarget copiedExitBranch = new UnconditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = newBasicBlock;
                newBasicBlock.exitBranch = copiedExitBranch;

                // search for all semantically equal basic blocks in the modified method CFG (as possible targets)
                searchSementicId = exitBranch.takenTarget.semanticId;
                List<BasicBlock> possibleTargetBasicBlocks = new List<BasicBlock>();
                foreach (BasicBlock searchBasicBlock in methodCfg.basicBlocks) {
                    if (searchBasicBlock.semanticId == searchSementicId) {
                        possibleTargetBasicBlocks.Add(searchBasicBlock);
                    }
                }
                if (possibleTargetBasicBlocks.Count() == 0) {
                    throw new ArgumentException("Not able to find any semantically equal basic block in CFG.");
                }

                // use a random possible basic block as target
                int randPosition = this.prng.Next(possibleTargetBasicBlocks.Count());
                BasicBlock targetBasicBlock = possibleTargetBasicBlocks.ElementAt(randPosition);

                // set basic block as target for the branch
                targetBasicBlock.entryBranches.Add(copiedExitBranch);
                copiedExitBranch.takenTarget = targetBasicBlock;

            }

            else if (copiedOriginalTargetBasicBlock.exitBranch as ConditionalBranchTarget != null) {
                ConditionalBranchTarget exitBranch = (copiedOriginalTargetBasicBlock.exitBranch as ConditionalBranchTarget);

                // create a copied exit branch
                ConditionalBranchTarget copiedExitBranch = new ConditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = newBasicBlock;
                newBasicBlock.exitBranch = copiedExitBranch;

                // search for all semantically equal basic blocks in the modified method CFG (as possible targets) (for "taken" branch)
                searchSementicId = exitBranch.takenTarget.semanticId;
                List<BasicBlock> possibleTargetBasicBlocks = new List<BasicBlock>();
                foreach (BasicBlock searchBasicBlock in methodCfg.basicBlocks) {
                    if (searchBasicBlock.semanticId == searchSementicId) {
                        possibleTargetBasicBlocks.Add(searchBasicBlock);
                    }
                }
                if (possibleTargetBasicBlocks.Count() == 0) {
                    throw new ArgumentException("Not able to find any semantically equal basic block in CFG.");
                }

                // use a random possible basic block as target
                int randPosition = this.prng.Next(possibleTargetBasicBlocks.Count());
                BasicBlock targetBasicBlock = possibleTargetBasicBlocks.ElementAt(randPosition);

                // set basic block as target for the branch
                targetBasicBlock.entryBranches.Add(copiedExitBranch);
                copiedExitBranch.takenTarget = targetBasicBlock;

                // search for all semantically equal basic blocks in the modified method CFG (as possible targets) (for "not taken" branch)
                searchSementicId = exitBranch.notTakenTarget.semanticId;
                possibleTargetBasicBlocks = new List<BasicBlock>();
                foreach (BasicBlock searchBasicBlock in methodCfg.basicBlocks) {
                    if (searchBasicBlock.semanticId == searchSementicId) {
                        possibleTargetBasicBlocks.Add(searchBasicBlock);
                    }
                }
                if (possibleTargetBasicBlocks.Count() == 0) {
                    throw new ArgumentException("Not able to find any semantically equal basic block in CFG.");
                }

                // use a random possible basic block as target
                randPosition = this.prng.Next(possibleTargetBasicBlocks.Count());
                targetBasicBlock = possibleTargetBasicBlocks.ElementAt(randPosition);

                // set basic block as target for the branch
                targetBasicBlock.entryBranches.Add(copiedExitBranch);
                copiedExitBranch.notTakenTarget = targetBasicBlock;

            }

            else if (copiedOriginalTargetBasicBlock.exitBranch as SwitchBranchTarget != null) {
                throw new ArgumentException("Not implemented yet.");
            }

            else if (copiedOriginalTargetBasicBlock.exitBranch as ExitBranchTarget != null) {

                // create a copied exit branch
                ExitBranchTarget copiedExitBranch = new ExitBranchTarget();
                copiedExitBranch.sourceBasicBlock = newBasicBlock;
                newBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (copiedOriginalTargetBasicBlock.exitBranch as ThrowBranchTarget != null) {
                throw new ArgumentException("Not implemented yet.");
            }

            else if (copiedOriginalTargetBasicBlock.exitBranch as TryBlockTarget != null) {
                throw new ArgumentException("Not implemented yet.");
            }

            else if (copiedOriginalTargetBasicBlock.exitBranch as ExceptionBranchTarget != null) {
                throw new ArgumentException("Not implemented yet.");
            }

            else {
                throw new ArgumentException("Do not know how to handle exit branch.");
            }

            return newBasicBlock;

        }


        // this function injects an opaque predicate to the target branch
        private void injectOpaquePredicateCode(CfgManipulator cfgManipulator, Target target, ref ConditionalBranchTarget opaqueExitBranch, IBranchTarget targetBranch, BasicBlockGraphNodeLink link, int currentValidPathId, int opaquePredicate, LocalDefinition currentNodeLocal) {

            // create "true" or "false" opaque predicate and add it to the code
            switch (opaquePredicate) {

                // "true" opaque predicate
                case 0: {
                        BasicBlock opaqueStartBasicBlock = null;

                        this.createCodeTruePredicate(ref opaqueStartBasicBlock, ref opaqueExitBranch, link, currentNodeLocal);

                        // inject target branch with opaque predicate
                        if ((targetBranch as SwitchBranchTarget) != null) {
                            SwitchBranchTarget tempTargetBranch = (targetBranch as SwitchBranchTarget);
                            cfgManipulator.insertBasicBlockBetweenBranch(tempTargetBranch, currentValidPathId, opaqueStartBasicBlock, opaqueExitBranch, true);
                        }
                        else {
                            throw new ArgumentException("Not implemented yet.");
                        }

                        // set template semantic id of dead code basic block
                        BasicBlock deadCodeBasicBlock = opaqueExitBranch.notTakenTarget;
                        GraphTransformerDeadCodeBasicBlock deadCodeMetadata = (deadCodeBasicBlock.transformationMetadata.ElementAt(0) as GraphTransformerDeadCodeBasicBlock);
                        deadCodeMetadata.semanticId = target.basicBlock.semanticId;

                        break;
                    }

                // "false" opaque predicate
                case 1: {
                        BasicBlock opaqueStartBasicBlock = null;

                        this.createCodeFalsePredicate(ref opaqueStartBasicBlock, ref opaqueExitBranch, link, currentNodeLocal);

                        // inject target branch with opaque predicate
                        if ((targetBranch as SwitchBranchTarget) != null) {
                            SwitchBranchTarget tempTargetBranch = (targetBranch as SwitchBranchTarget);
                            cfgManipulator.insertBasicBlockBetweenBranch(tempTargetBranch, currentValidPathId, opaqueStartBasicBlock, opaqueExitBranch, false);
                        }
                        else {
                            throw new ArgumentException("Not implemented yet.");
                        }

                        // set template semantic id of dead code basic block
                        BasicBlock deadCodeBasicBlock = opaqueExitBranch.takenTarget;
                        GraphTransformerDeadCodeBasicBlock deadCodeMetadata = (deadCodeBasicBlock.transformationMetadata.ElementAt(0) as GraphTransformerDeadCodeBasicBlock);
                        deadCodeMetadata.semanticId = target.basicBlock.semanticId;

                        break;
                    }

                // "Random" opaque predicate.
                case 2: {
                        BasicBlock opaqueStartBasicBlock = null;

                        if((targetBranch as SwitchBranchTarget) != null) {
                            SwitchBranchTarget tempTargetBranch = (targetBranch as SwitchBranchTarget);
                            var destinationBlock = tempTargetBranch.takenTarget[currentValidPathId];

                            this.createCodeRandomPredicate(ref opaqueStartBasicBlock, ref opaqueExitBranch, link, currentNodeLocal);

                            bool chooseBranch = prng.Next(1) == 0;
                            cfgManipulator.insertBasicBlockBetweenBranch(tempTargetBranch, currentValidPathId, opaqueStartBasicBlock, opaqueExitBranch, chooseBranch);
                        }
                        else {
                            throw new ArgumentException("Not implemented yet.");
                        }
                        break;
                    }

                default:
                    throw new ArgumentException("This case should never be reached.");
            }

        }


        // this function injects an opaque predicate to the target branch
        private void injectOpaquePredicateCode(CfgManipulator cfgManipulator, MethodCfg methodCfg, BasicBlock targetBasicBlock, IBranchTarget targetBranch, BasicBlockGraphNodeLink link, int opaquePredicate, LocalDefinition currentNodeLocal) {

            int currentValidPathId = link.validPathId;
            ConditionalBranchTarget opaqueExitBranch = null;

            // create "true" or "false" opaque predicate and add it to the code
            switch (opaquePredicate) {

                // "true" opaque predicate
                case 0: {
                        BasicBlock opaqueStartBasicBlock = null;

                        this.createCodeTruePredicate(ref opaqueStartBasicBlock, ref opaqueExitBranch, link, currentNodeLocal);

                        // inject target branch with opaque predicate
                        if ((targetBranch as SwitchBranchTarget) != null) {
                            SwitchBranchTarget tempTargetBranch = (targetBranch as SwitchBranchTarget);
                            cfgManipulator.insertBasicBlockBetweenBranch(tempTargetBranch, currentValidPathId, opaqueStartBasicBlock, opaqueExitBranch, true);
                        }
                        else if ((targetBranch as UnconditionalBranchTarget) != null) {
                            UnconditionalBranchTarget tempTargetBranch = (targetBranch as UnconditionalBranchTarget);
                            cfgManipulator.insertBasicBlockBetweenBranch(tempTargetBranch, opaqueStartBasicBlock, opaqueExitBranch, true);
                        }
                        else {
                            throw new ArgumentException("Not implemented yet.");
                        }

                        // set template semantic id of dead code basic block
                        BasicBlock deadCodeBasicBlock = opaqueExitBranch.notTakenTarget;
                        GraphTransformerDeadCodeBasicBlock deadCodeMetadata = (deadCodeBasicBlock.transformationMetadata.ElementAt(0) as GraphTransformerDeadCodeBasicBlock);
                        deadCodeMetadata.semanticId = targetBasicBlock.semanticId;

                        // create intermediate basic block and inject it between correct opaque predicate branch and target basic block
                        BasicBlock intermediateBasicBlock = new BasicBlock();
                        intermediateBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Br, 0));
                        methodCfg.basicBlocks.Add(intermediateBasicBlock);

                        // add metadata to intermediate basic block
                        GraphTransformerIntermediateBasicBlock metadataIntermediate = new GraphTransformerIntermediateBasicBlock();
                        metadataIntermediate.correspondingGraphNodes.Add(link);
                        intermediateBasicBlock.transformationMetadata.Add(metadataIntermediate);

                        // create intermediate exit branch
                        UnconditionalBranchTarget intermediateBasicBlockExitBranch = new UnconditionalBranchTarget();
                        intermediateBasicBlockExitBranch.sourceBasicBlock = intermediateBasicBlock;
                        intermediateBasicBlock.exitBranch = intermediateBasicBlockExitBranch;

                        cfgManipulator.insertBasicBlockBetweenBranch(opaqueExitBranch, true, intermediateBasicBlock, intermediateBasicBlockExitBranch);

                        break;
                    }

                // "false" opaque predicate
                case 1: {
                        BasicBlock opaqueStartBasicBlock = null;

                        this.createCodeFalsePredicate(ref opaqueStartBasicBlock, ref opaqueExitBranch, link, currentNodeLocal);

                        // inject target branch with opaque predicate
                        if ((targetBranch as SwitchBranchTarget) != null) {
                            SwitchBranchTarget tempTargetBranch = (targetBranch as SwitchBranchTarget);
                            cfgManipulator.insertBasicBlockBetweenBranch(tempTargetBranch, currentValidPathId, opaqueStartBasicBlock, opaqueExitBranch, false);
                        }
                        else if ((targetBranch as UnconditionalBranchTarget) != null) {
                            UnconditionalBranchTarget tempTargetBranch = (targetBranch as UnconditionalBranchTarget);
                            cfgManipulator.insertBasicBlockBetweenBranch(tempTargetBranch, opaqueStartBasicBlock, opaqueExitBranch, false);
                        }
                        else {
                            throw new ArgumentException("Not implemented yet.");
                        }

                        // set template semantic id of dead code basic block
                        BasicBlock deadCodeBasicBlock = opaqueExitBranch.takenTarget;
                        GraphTransformerDeadCodeBasicBlock deadCodeMetadata = (deadCodeBasicBlock.transformationMetadata.ElementAt(0) as GraphTransformerDeadCodeBasicBlock);
                        deadCodeMetadata.semanticId = targetBasicBlock.semanticId;

                        // create intermediate basic block and inject it between correct opaque predicate branch and target basic block
                        BasicBlock intermediateBasicBlock = new BasicBlock();
                        intermediateBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Br, 0));
                        methodCfg.basicBlocks.Add(intermediateBasicBlock);

                        // add metadata to intermediate basic block
                        GraphTransformerIntermediateBasicBlock metadataIntermediate = new GraphTransformerIntermediateBasicBlock();
                        metadataIntermediate.correspondingGraphNodes.Add(link);
                        intermediateBasicBlock.transformationMetadata.Add(metadataIntermediate);

                        // create intermediate exit branch
                        UnconditionalBranchTarget intermediateBasicBlockExitBranch = new UnconditionalBranchTarget();
                        intermediateBasicBlockExitBranch.sourceBasicBlock = intermediateBasicBlock;
                        intermediateBasicBlock.exitBranch = intermediateBasicBlockExitBranch;

                        cfgManipulator.insertBasicBlockBetweenBranch(opaqueExitBranch, false, intermediateBasicBlock, intermediateBasicBlockExitBranch);

                        break;
                    }

                // "Random" opaque predicate.
                case 2: {
                        BasicBlock opaqueStartBasicBlock = null;

                        if ((targetBranch as SwitchBranchTarget) != null) {
                            SwitchBranchTarget tempTargetBranch = (targetBranch as SwitchBranchTarget);
                            var destinationBlock = tempTargetBranch.takenTarget[currentValidPathId];

                            this.createCodeRandomPredicate(ref opaqueStartBasicBlock, ref opaqueExitBranch, link, currentNodeLocal);

                            bool chooseBranch = prng.Next(1) == 0;
                            cfgManipulator.insertBasicBlockBetweenBranch(tempTargetBranch, currentValidPathId, opaqueStartBasicBlock, opaqueExitBranch, chooseBranch);
                        }
                        else {
                            throw new ArgumentException("Not implemented yet.");
                        }
                        break;
                    }

                default:
                    throw new ArgumentException("This case should never be reached.");
            }

        }


        // this function searches for the next node of the given valid path id and the node idx for this node
        private void getNextNode(ref NodeObject nextNode, ref int nextNodeIdx, NodeObject[] currentNodes, int currentValidPathId) {

            // check if a child node exists
            // => if not set next node to starting node
            if (currentNodes[currentValidPathId].nodeObjects[0] == null) {
                nextNode = this.graph.startingNode;
                nextNodeIdx = this.prng.Next(this.graphDimension);
            }
            else {
                for (int i = 0; i < currentNodes[currentValidPathId].nodeObjects.Count(); i++) {

                    // if the child node does not belong to a valid path
                    // => skip it
                    if (currentNodes[currentValidPathId].nodeObjects[i].elementOfValidPath.Count() == 0) {
                        continue;
                    }

                    // the child node belongs to a valid path
                    // => check if it is the correct valid path
                    else {
                        // if the child node belongs to the correct valid path
                        // => set next node to it
                        if (currentNodes[currentValidPathId].nodeObjects[i].elementOfValidPath.Contains(currentValidPathId)) {
                            nextNode = currentNodes[currentValidPathId].nodeObjects[i];
                            nextNodeIdx = i;
                            break;
                        }
                    }
                }
                if (nextNode == null) {
                    throw new ArgumentException("Not able to find correct child node.");
                }
            }

        }


        // this function generates and injects code into the given entry branch that moves the pointer from the source node to the target node
        private void injectLinkMoveCode(CfgManipulator cfgManipulator, BasicBlockGraphNodeLink startGraphLink, BasicBlockGraphNodeLink targetGraphLink, IBranchTarget entryBranch, int currentValidPathId, LocalDefinition currentNodeLocal) {

            NodeObject targetNode = targetGraphLink.graphNode;
            NodeObject startNode = startGraphLink.graphNode;
            IBranchTarget branchToManipulate = entryBranch;

            // do nothing if the source node is already the same as the target node
            if (startNode == targetNode) {
                return;
            }

            // check if the pointer have to be moved to the start pointer before it can be moved to the target element
            bool goToStart = false;
            if (startNode.positionInGraph.Count() >= targetNode.positionInGraph.Count()) {
                goToStart = true;
            }
            else {
                for (int idx = 0; idx < startNode.positionInGraph.Count(); idx++) {
                    if (startNode.positionInGraph.ElementAt(idx) != targetNode.positionInGraph.ElementAt(idx)) {
                        goToStart = true;
                        break;
                    }
                }
            }

            // set current graph link (is used for the metadata of the pointer correcting basic blocks
            BasicBlockGraphNodeLink currentGraphLink = new BasicBlockGraphNodeLink(startGraphLink.graphNode, currentValidPathId);

            // check how to move the pointer to the target element
            // => first go to the start pointer
            if (goToStart) {

                // generate and inject code that moves the pointer on a random path forward until the start node of the graph is reached
                while (startNode != this.graph.startingNode) {

                    // generate code to move the pointer to the next node in the graph and inject it
                    int nextNodeIdx = this.prng.Next(this.graphDimension);
                    UnconditionalBranchTarget injectedExitBranch = new UnconditionalBranchTarget();
                    BasicBlock injectedBasicBlock = this.createCodeNextNode(injectedExitBranch, currentGraphLink, currentNodeLocal, nextNodeIdx, true);
                    if ((branchToManipulate as UnconditionalBranchTarget) != null) {
                        UnconditionalBranchTarget tempBranchToManipulate = (branchToManipulate as UnconditionalBranchTarget);
                        cfgManipulator.insertBasicBlockBetweenBranch(tempBranchToManipulate, injectedBasicBlock, injectedExitBranch);
                    }
                    else if ((branchToManipulate as SwitchBranchTarget) != null) {
                        SwitchBranchTarget tempBranchToManipulate = (branchToManipulate as SwitchBranchTarget);
                        cfgManipulator.insertBasicBlockBetweenBranch(tempBranchToManipulate, currentValidPathId, injectedBasicBlock, injectedExitBranch);
                    }
                    else {
                        throw new ArgumentException("Not yet implemented."); // TODO
                    }

                    // set the next branch to manipulate as the newly created exit branch
                    branchToManipulate = injectedExitBranch;

                    // move pointer to the next node in the graph or to the start node if it does not exist
                    if (startNode.nodeObjects[nextNodeIdx] == null) {
                        startNode = this.graph.startingNode;
                    }
                    else {
                        startNode = startNode.nodeObjects[nextNodeIdx];
                    }

                    // create new graph link for the current node
                    currentGraphLink = new BasicBlockGraphNodeLink(startNode, currentValidPathId);
                }

                // generate and inject code that moves the pointer to the correct position of the given graph node
                foreach (int nextNodeIdx in targetNode.positionInGraph) {

                    // generate code to move the pointer to the next node in the graph and inject it
                    UnconditionalBranchTarget injectedExitBranch = new UnconditionalBranchTarget();
                    BasicBlock injectedBasicBlock = this.createCodeNextNode(injectedExitBranch, currentGraphLink, currentNodeLocal, nextNodeIdx, true);
                    if ((branchToManipulate as UnconditionalBranchTarget) != null) {
                        UnconditionalBranchTarget tempBranchToManipulate = (branchToManipulate as UnconditionalBranchTarget);
                        cfgManipulator.insertBasicBlockBetweenBranch(tempBranchToManipulate, injectedBasicBlock, injectedExitBranch);
                    }
                    else if ((branchToManipulate as SwitchBranchTarget) != null) {
                        SwitchBranchTarget tempBranchToManipulate = (branchToManipulate as SwitchBranchTarget);
                        cfgManipulator.insertBasicBlockBetweenBranch(tempBranchToManipulate, currentValidPathId, injectedBasicBlock, injectedExitBranch);
                    }
                    else {
                        throw new ArgumentException("Not yet implemented."); // TODO
                    }

                    // set the next branch to manipulate as the newly created exit branch
                    branchToManipulate = injectedExitBranch;

                    // move pointer to the next node in the graph or to the start node if it does not exist
                    if (startNode.nodeObjects[nextNodeIdx] == null) {
                        startNode = this.graph.startingNode;
                    }
                    else {
                        startNode = startNode.nodeObjects[nextNodeIdx];
                    }

                    // create new graph link for the current node
                    currentGraphLink = new BasicBlockGraphNodeLink(startNode, currentValidPathId);
                }
            }

            // => go on a direct path to the target element
            else {

                // generate and inject code that moves the pointer to the correct position of the given graph node
                for (int idx = startNode.positionInGraph.Count(); idx < targetNode.positionInGraph.Count(); idx++) {

                    // generate code to move the pointer to the next node in the graph and inject it
                    UnconditionalBranchTarget injectedExitBranch = new UnconditionalBranchTarget();
                    BasicBlock injectedBasicBlock = this.createCodeNextNode(injectedExitBranch, currentGraphLink, currentNodeLocal, targetNode.positionInGraph.ElementAt(idx), true);
                    if ((branchToManipulate as UnconditionalBranchTarget) != null) {
                        UnconditionalBranchTarget tempBranchToManipulate = (branchToManipulate as UnconditionalBranchTarget);
                        cfgManipulator.insertBasicBlockBetweenBranch(tempBranchToManipulate, injectedBasicBlock, injectedExitBranch);
                    }
                    else if ((branchToManipulate as SwitchBranchTarget) != null) {
                        SwitchBranchTarget tempBranchToManipulate = (branchToManipulate as SwitchBranchTarget);
                        cfgManipulator.insertBasicBlockBetweenBranch(tempBranchToManipulate, currentValidPathId, injectedBasicBlock, injectedExitBranch);
                    }
                    else {
                        throw new ArgumentException("Not yet implemented."); // TODO
                    }

                    // set the next branch to manipulate as the newly created exit branch
                    branchToManipulate = injectedExitBranch;

                    // move pointer to the next node in the graph or to the start node if it does not exist
                    if (startNode.nodeObjects[targetNode.positionInGraph.ElementAt(idx)] == null) {
                        startNode = this.graph.startingNode;
                    }
                    else {
                        startNode = startNode.nodeObjects[targetNode.positionInGraph.ElementAt(idx)];
                    }

                    // create new graph link for the current node
                    currentGraphLink = new BasicBlockGraphNodeLink(startNode, currentValidPathId);
                }

            }

            // check if the final reached node is the same as the target node
            if (startNode != targetNode) {
                throw new ArgumentException("Current node and target node are not the same.");
            }

        }


        // this functions creates metadata for the given basic block and adds it
        private bool createAndAddMetadata(BasicBlock currentBasicBlock) {

            // get metadata of entry basic block (if exists)
            IGraphTransformerMetadata previousMetadata = null;
            BasicBlock previousBasicBlock = null;
            IBranchTarget previousExitBranch = null;
            for (int i = 0; i < currentBasicBlock.entryBranches.Count(); i++) {

                previousBasicBlock = currentBasicBlock.entryBranches.ElementAt(i).sourceBasicBlock;

                for (int j = 0; j < previousBasicBlock.transformationMetadata.Count(); j++) {
                    // get graph transformer metadata for basic blocks
                    if ((previousBasicBlock.transformationMetadata.ElementAt(j) as IGraphTransformerMetadata) == null) {
                        continue;
                    }
                    previousMetadata = (previousBasicBlock.transformationMetadata.ElementAt(j) as IGraphTransformerMetadata);
                    previousExitBranch = currentBasicBlock.entryBranches.ElementAt(i);
                    break;
                }
                if (previousMetadata != null) {
                    break;
                }
            }
            // skip basic block if there is no metadata in one of the entry basic blocks
            if (previousMetadata == null) { // TODO: here is room for optimization => do not skip it
                return false;
            }
            List<BasicBlockGraphNodeLink> previousLinks = previousMetadata.correspondingGraphNodes;

            // choose the next nodes of the obfuscation graph for the current basic block
            NodeObject[] currentNodes = new NodeObject[this.graph.graphValidPathCount];

            // check if previous basic block has links to the obfuscation graph for all existing valid paths
            // (if previous basic block for example was a "state change" basic block it can only have one link to the obfuscation graph)
            // => just copy obfuscation graph nodes of previous basic block if it contains all valid paths
            if (previousLinks.Count() == this.graph.graphValidPathCount) {
                for (int currentValidPathId = 0; currentValidPathId < this.graph.graphValidPathCount; currentValidPathId++) {
                    bool found = false;
                    for (int i = 0; i < previousLinks.Count(); i++) {
                        if (previousLinks.ElementAt(i).validPathId == currentValidPathId) {
                            currentNodes[currentValidPathId] = previousLinks.ElementAt(i).graphNode;
                            found = true;
                            break;
                        }
                    }
                    if (!found) {
                        throw new ArgumentException("Was not able to find link to obfuscation graph in previous basic block for at least one valid path.");
                    }
                }
            }

            // previous basic block does not contain all valid paths
            // => copy all existing valid paths links and choose random links for the missing ones
            else {

                for (int currentValidPathId = 0; currentValidPathId < this.graph.graphValidPathCount; currentValidPathId++) {
                    bool found = false;
                    for (int i = 0; i < previousLinks.Count(); i++) {
                        if (previousLinks.ElementAt(i).validPathId == currentValidPathId) {
                            currentNodes[currentValidPathId] = previousLinks.ElementAt(i).graphNode;
                            found = true;
                            break;
                        }
                    }

                    // if a link for the current valid path does not exist
                    // => choose randomly a node in the obfuscation graph on the current valid path
                    if (!found) {
                        int moveCount = this.prng.Next(this.graphDepth);
                        NodeObject nextNode = this.graph.startingNode;
                        for (int move = 0; move < moveCount; move++) {
                            for (int i = 0; i < nextNode.nodeObjects.Count(); i++) {

                                // if the child node does not belong to a valid path
                                // => skip it
                                if (nextNode.nodeObjects[i].elementOfValidPath.Count() == 0) {
                                    continue;
                                }

                                // the child node belongs to a valid path
                                // => check if it is the correct valid path
                                else {
                                    // if the child node belongs to the correct valid path
                                    // => set next node to it
                                    if (nextNode.nodeObjects[i].elementOfValidPath.Contains(currentValidPathId)) {
                                        nextNode = nextNode.nodeObjects[i];
                                        break;
                                    }
                                }
                            }
                            if (nextNode == null) {
                                throw new ArgumentException("Not able to find correct child node.");
                            }
                        }

                        currentNodes[currentValidPathId] = nextNode;

                    }
                }
            }

            NodeObject[] nextNodes = new NodeObject[this.graph.graphValidPathCount];
            for (int currentValidPathId = 0; currentValidPathId < currentNodes.Count(); currentValidPathId++) {

                // choose randomly if the pointer of the current valid path should be moved forward
                // => move the pointer of the current valid path forward
                if (this.prng.Next(2) == 0) {

                    // get the next node of the valid path
                    NodeObject nextNode = null;
                    int nextNodeIdx = 0;
                    this.getNextNode(ref nextNode, ref nextNodeIdx, currentNodes, currentValidPathId);
                    nextNodes[currentValidPathId] = nextNode;

                }

                // => do not move the pointer of the current valid path forward
                else {

                    // set next node in the graph to the current one (it was not changed)
                    nextNodes[currentValidPathId] = currentNodes[currentValidPathId];

                }
            }

            // create transformation metadata for the current basic block
            GraphTransformerMetadataBasicBlock metadata = new GraphTransformerMetadataBasicBlock();

            for (int currentValidPathId = 0; currentValidPathId < this.graph.graphValidPathCount; currentValidPathId++) {
                BasicBlockGraphNodeLink link = new BasicBlockGraphNodeLink(nextNodes[currentValidPathId], currentValidPathId);
                metadata.correspondingGraphNodes.Add(link);
            }

            currentBasicBlock.transformationMetadata.Add(metadata);

            return true;

        }


        // this function is used to fill the created dead code basic blocks with actual dead code
        private void fillDeadCodeBasicBlocks(MethodCfg methodCfg) {

            var manipulator = new CfgManipulator(this.module, this.host, this.logger, methodCfg);
            CodeGenerator codeGenerator = new CodeGenerator(this.module, this.host, this.logger, this.prng, methodCfg, this.debugging, manipulator);
            //CodeGenerator codeGenerator = new CodeGenerator(this.module, this.host, this.logger, this.prng, methodCfg, true, manipulator);

            // search through all basic blocks if there are dead code basic blocks
            foreach (BasicBlock currentBasicBlock in methodCfg.basicBlocks) {

                // fill basic block with dead code if it is marked as a dead code basic block
                foreach (ITransformationMetadata metadata in currentBasicBlock.transformationMetadata) {
                    if ((metadata as GraphTransformerDeadCodeBasicBlock) != null) {
                        codeGenerator.generateDeadCode(currentBasicBlock, metadata);
                    }
                }

            }

        }


        // Searches semantically equivalent blocks in the given method CFG and obfuscates them.
        private void obfuscateSemanticallyEquivalentBlocks(MethodCfg methodCfg)
        {
            // Group all blocks with same semantic ID.
            var semanticGroups = new Dictionary<int, List<BasicBlock>>();

            foreach(var block in methodCfg.basicBlocks) {
                List<BasicBlock> blockList = null;
                if(!semanticGroups.TryGetValue(block.semanticId, out blockList)) {
                    blockList = new List<BasicBlock>();
                    semanticGroups[block.semanticId] = blockList;
                }

                if(block.semanticId != -1) {
                    semanticGroups[block.semanticId].Add(block);
                }
            }

            // Mutate equivalent blocks.
            var manipulator = new CfgManipulator(this.module, this.host, this.logger, methodCfg);
            var codeMutator = new CodeMutator(this.module, this.host, this.logger, this.prng, methodCfg, manipulator, this.debugging);

            foreach(var blockList in semanticGroups.Values) {
                codeMutator.mutateSemanticallyEquivalentBlocks(blockList); 
            }
        }


        // Mutates all blocks in the given method CFG.
        private void mutateBlocks(MethodCfg methodCfg)
        {
            var manipulator = new CfgManipulator(this.module, this.host, this.logger, methodCfg);
            var codeMutator = new CodeMutator(this.module, this.host, this.logger, this.prng, methodCfg, manipulator, this.debugging);

            codeMutator.mutateBlocks(methodCfg.basicBlocks);
        }


        public void addObfuscationToMethod(MethodCfg methodCfg) {

            MethodCfg copyMethodCfg = new MethodCfg(methodCfg);
            CfgManipulator cfgManipulator = new CfgManipulator(this.module, this.host, this.logger, methodCfg);
            GraphRandomStateGenerator randomStateGenerator = new GraphRandomStateGenerator(this.module, this.host, this.logger, cfgManipulator, this.helperClass, this.graph, methodCfg, this.debugging);

            // DEBUG
            if (this.debugging) {
                //this.logger.dumpMethodCfg(methodCfg, this.debuggingNumber.ToString().PadLeft(3, '0') + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method));
                this.debuggingNumber++;
            }

            // add local state variable
            LocalDefinition intStateLocal = new LocalDefinition();
            intStateLocal.IsReference = false;
            intStateLocal.IsPinned = false;
            intStateLocal.IsModified = false;
            intStateLocal.Type = this.host.PlatformType.SystemInt32;
            intStateLocal.MethodDefinition = methodCfg.method;
            cfgManipulator.addLocalVariable(intStateLocal);

            // add local current graph node variable
            LocalDefinition currentNodeLocal = new LocalDefinition();
            currentNodeLocal.IsReference = false;
            currentNodeLocal.IsPinned = false;
            currentNodeLocal.IsModified = false;
            currentNodeLocal.Type = this.nodeInterface;
            currentNodeLocal.MethodDefinition = methodCfg.method;
            cfgManipulator.addLocalVariable(currentNodeLocal);

            // create new basic block that sets the current node to the start node and get the state
            // (will be the new starting basic block of the method)
            BasicBlock startBasicBlock = new BasicBlock();
            startBasicBlock.startIdx = 0;
            startBasicBlock.endIdx = 0;

            // set the current node pointer to the start node
            startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
            startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.interfaceStartPointerGet));
            startBasicBlock.operations.Add(this.helperClass.createNewOperation(OperationCode.Stloc, currentNodeLocal));

            // initialize the random state generator
            List<IOperation> tempOperations = randomStateGenerator.generateCodeInitializeRandomState();
            startBasicBlock.operations.AddRange(tempOperations);

            // set global state
            tempOperations = randomStateGenerator.generateCodeSetRandomState(intStateLocal);
            startBasicBlock.operations.AddRange(tempOperations);

            // create exit branch for the new start basic block
            NoBranchTarget startExitBranch = new NoBranchTarget();
            startExitBranch.sourceBasicBlock = startBasicBlock;
            startBasicBlock.exitBranch = startExitBranch;

            // set the original start basic block as the target of the exit branch
            startExitBranch.takenTarget = methodCfg.startBasicBlock;
            methodCfg.startBasicBlock.entryBranches.Add(startExitBranch);

            // set new start basic block as start basic block of the method cfg
            methodCfg.startBasicBlock = startBasicBlock;
            methodCfg.basicBlocks.Add(startBasicBlock);

            // obfuscate the control flow
            this.addMetadataIter(cfgManipulator, randomStateGenerator, copyMethodCfg, methodCfg, startBasicBlock, intStateLocal, currentNodeLocal);
            this.processExitBranchesIter(cfgManipulator, randomStateGenerator, copyMethodCfg, methodCfg, startBasicBlock, intStateLocal, currentNodeLocal);
            this.addOpaquePredicatesIter(cfgManipulator, randomStateGenerator, copyMethodCfg, methodCfg, startBasicBlock, intStateLocal, currentNodeLocal);
            this.removeReplaceUnconditionalBranchesIter(cfgManipulator, randomStateGenerator, copyMethodCfg, methodCfg, startBasicBlock, intStateLocal, currentNodeLocal);

            // fill dead code basic blocks with actual dead code
            this.fillDeadCodeBasicBlocks(methodCfg);
            this.obfuscateSemanticallyEquivalentBlocks(methodCfg);

            // TODO QUICK FIX
            // mutateBlocks() does not work when debugging is deactivated
            // but since we only focus on probabilistic control flow, ignore it for now
            this.debugging = true;
            this.mutateBlocks(methodCfg);
            this.debugging = false;
            
            // if debugging is activated => add code that allows to trace the control flow through the method
            if (this.debugging
                || this.trace) {

                List<IOperation> traceWriterOperations = null;

                // add code to the beginning of each basic block that writes to the trace file
                for (int idx = 0; idx < methodCfg.basicBlocks.Count(); idx++) {

                    traceWriterOperations = new List<IOperation>();
                    traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                    traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldfld, this.debuggingTraceWriter));
                    traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, "BB" + idx.ToString() + ";ID" + methodCfg.basicBlocks.ElementAt(idx).id.ToString()));
                    traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.helperClass.textWriterWriteLine));

                    methodCfg.basicBlocks.ElementAt(idx).operations.InsertRange(0, traceWriterOperations);

                    // check if the basic block has an exit branch
                    // => add code that closes the file handler of the trace file before the ret instruction
                    if ((methodCfg.basicBlocks.ElementAt(idx).exitBranch as ExitBranchTarget) != null) {

                        traceWriterOperations = new List<IOperation>();

                        traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                        traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldfld, this.debuggingTraceWriter));
                        traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.helperClass.textWriterClose));

                        methodCfg.basicBlocks.ElementAt(idx).operations.InsertRange(methodCfg.basicBlocks.ElementAt(idx).operations.Count() - 1, traceWriterOperations);

                    }
                }

                // create local integer variable needed for the trace file code
                LocalDefinition intLocal = new LocalDefinition();
                intLocal.IsReference = false;
                intLocal.IsPinned = false;
                intLocal.IsModified = false;
                intLocal.Type = this.host.PlatformType.SystemInt32;
                intLocal.MethodDefinition = methodCfg.method;
                cfgManipulator.addLocalVariable(intLocal);

                traceWriterOperations = new List<IOperation>();

                // get the hash code of this instance as unique object id                    
                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.helperClass.objectGetHashCode));
                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Stloc, intLocal));

                // initialize io stream writer for trace file (use object hash as id for this trace and only append)
                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));

                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, this.debuggingDumpLocation + "\\" + this.debuggingTraceFilePrefix + "_"));

                // cast random integer to string and store in object id
                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldloca, intLocal));
                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Call, this.helperClass.int32ToString));

                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, ".txt"));
                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Call, this.helperClass.stringConcatThree));

                // only append to file
                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldc_I4_1));

                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Newobj, this.helperClass.streamWriterCtorAppend));
                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Stfld, this.debuggingTraceWriter));

                // write marker to trace file that marks the beginning of this trace
                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldfld, this.debuggingTraceWriter));
                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, "\n------NEWSTART------\n"));
                traceWriterOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.helperClass.textWriterWriteLine));

                // add code to the beginning of the start basic block
                methodCfg.startBasicBlock.operations.InsertRange(0, traceWriterOperations);

            }

            logger.dumpMethodCfg(copyMethodCfg, "copy_final");

            return;
        }


        private void addMetadataIter(CfgManipulator cfgManipulator, GraphRandomStateGenerator randomStateGenerator, MethodCfg originalMethodCfg, MethodCfg methodCfg, BasicBlock startBasicBlock, LocalDefinition intStateLocal, LocalDefinition currentNodeLocal) {

            int currentValidPathId;

            // create transformation metadata for the start basic block
            GraphTransformerMetadataBasicBlock metadata = new GraphTransformerMetadataBasicBlock();

            for (currentValidPathId = 0; currentValidPathId < this.graph.graphValidPathCount; currentValidPathId++) {
                BasicBlockGraphNodeLink link = new BasicBlockGraphNodeLink(this.graph.startingNode, currentValidPathId);
                metadata.correspondingGraphNodes.Add(link);
            }

            startBasicBlock.transformationMetadata.Add(metadata);

            // create a list of basic blocks that still have to be processed by the algorithm
            List<BasicBlock> basicBlocksToProcess = new List<BasicBlock>(methodCfg.basicBlocks);
            basicBlocksToProcess.Remove(startBasicBlock);

            System.Console.WriteLine("Basic Blocks to process (Metadata): " + basicBlocksToProcess.Count());

            // add metadata to all basic blocks
            while (true) {

                List<BasicBlock> copiedList = new List<BasicBlock>(basicBlocksToProcess);
                foreach (BasicBlock currentBasicBlock in copiedList) {

                    // check if the current basic block was already visited => it is already used for the graph obfuscation => skip it
                    bool hasGraphMetadata = false;
                    for (int i = 0; i < currentBasicBlock.transformationMetadata.Count(); i++) {
                        // get graph transformer metadata for basic blocks
                        if ((currentBasicBlock.transformationMetadata.ElementAt(i) as IGraphTransformerMetadata) == null) {
                            continue;
                        }
                        hasGraphMetadata = true;
                        break;
                    }
                    if (hasGraphMetadata) {
                        basicBlocksToProcess.Remove(currentBasicBlock);
                        continue;
                    }

                    if (!this.createAndAddMetadata(currentBasicBlock)) { 
                        continue;
                    }

                    // remove current basic block from the list of basic blocks to process
                    basicBlocksToProcess.Remove(currentBasicBlock);

                }

                System.Console.WriteLine("Basic Blocks to process (Metadata): " + basicBlocksToProcess.Count());

                // check if basic blocks exist to process => if not break loop
                if (basicBlocksToProcess.Count() == 0) {
                    break;
                }

            }

        }


        private void processExitBranchesIter(CfgManipulator cfgManipulator, GraphRandomStateGenerator randomStateGenerator, MethodCfg originalMethodCfg, MethodCfg methodCfg, BasicBlock startBasicBlock, LocalDefinition intStateLocal, LocalDefinition currentNodeLocal) {

            int currentValidPathId;

            // create a list of basic blocks that still have to be processed by the algorithm
            List<BasicBlock> basicBlocksToProcess = new List<BasicBlock>(methodCfg.basicBlocks);

            System.Console.WriteLine("Basic Blocks to process (Control Flow): " + basicBlocksToProcess.Count());

            while (true) {

                List<BasicBlock> copiedList = new List<BasicBlock>(basicBlocksToProcess);
                foreach (BasicBlock currentBasicBlock in copiedList) {

                    // get the metadata for the current basic block
                    GraphTransformerMetadataBasicBlock metadata = null;
                    for (int i = 0; i < currentBasicBlock.transformationMetadata.Count(); i++) {
                        // get graph transformer metadata for basic blocks
                        if ((currentBasicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock) == null) {
                            continue;
                        }
                        metadata = (currentBasicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock);
                        break;
                    }
                    if (metadata == null) {
                        throw new ArgumentException("Not able to find metadata for current basic block.");
                    }

                    // remove currently processed basic block from the list of basic blocks to process
                    basicBlocksToProcess.Remove(currentBasicBlock);

                    // check if the current exit branch is of type "no branch target"
                    if (currentBasicBlock.exitBranch as NoBranchTarget != null) {

                        NoBranchTarget introBranch = (NoBranchTarget)currentBasicBlock.exitBranch;

                        // search for all semantically equal basic blocks in the modified method CFG (as possible targets)
                        int searchSementicId = introBranch.takenTarget.semanticId;
                        List<BasicBlock> possibleTargetBasicBlocks = new List<BasicBlock>();
                        if (searchSementicId != -1) {
                            foreach (BasicBlock searchBasicBlock in methodCfg.basicBlocks) {
                                if (searchBasicBlock.semanticId == searchSementicId) {
                                    possibleTargetBasicBlocks.Add(searchBasicBlock);
                                }
                            }
                            if (possibleTargetBasicBlocks.Count() == 0) {
                                throw new ArgumentException("Not able to find any semantically equal basic block in CFG.");
                            }
                        }
                        else {
                            possibleTargetBasicBlocks.Add(introBranch.takenTarget);
                        }

                        // create a list of target basic blocks of this branch
                        List<Target> listOfTargets = new List<Target>();
                        Target target = new Target();
                        target.basicBlock = introBranch.takenTarget;
                        listOfTargets.Add(target);

                        // generate code for the "state switch"
                        BasicBlock stateBasicBlock = null;
                        SwitchBranchTarget stateExitBranch = null;
                        this.createCodeStateSwitch(ref stateBasicBlock, ref stateExitBranch, intStateLocal, metadata.correspondingGraphNodes);
                        cfgManipulator.insertBasicBlockBetweenBranch(introBranch, stateBasicBlock, stateExitBranch, 0);

                        // create for each valid path a branch in the switch statement of the "state switch"
                        for (currentValidPathId = 0; currentValidPathId < this.graph.graphValidPathCount; currentValidPathId++) {

                            // ignore first valid path (with id 0)
                            // => add artificial branch to original branch target (for the current valid path id)
                            if (currentValidPathId != 0) {

                                // fill switch taken branch list with null when index is out of range
                                while (stateExitBranch.takenTarget.Count() <= currentValidPathId) {
                                    stateExitBranch.takenTarget.Add(null);
                                }

                                // randomly chose what the target of the current branch should be (weight to use an existing basic block to have less memory consumption)
                                switch (this.prng.Next(this.duplicateBasicBlockWeight)) {
                                    case 0:
                                    case 1:
                                    case 2:
                                    case 3:
                                    case 4:
                                    case 5: {

                                            // duplicate the target basic block
                                            BasicBlock duplicatedBasicBlock = this.duplicateBasicBlock(originalMethodCfg, methodCfg, listOfTargets.ElementAt(0).basicBlock);
                                            methodCfg.basicBlocks.Add(duplicatedBasicBlock);
                                            basicBlocksToProcess.Add(duplicatedBasicBlock);

                                            // add new basic block as target of the switch case
                                            stateExitBranch.takenTarget[currentValidPathId] = duplicatedBasicBlock;
                                            duplicatedBasicBlock.entryBranches.Add(stateExitBranch);

                                            // add new basic block to the list of target basic blocks
                                            target = new Target();
                                            target.basicBlock = duplicatedBasicBlock;
                                            listOfTargets.Add(target);

                                            // add duplicated basic block to the list of possible target basic blocks
                                            possibleTargetBasicBlocks.Add(duplicatedBasicBlock);

                                            // create metadata for the newly created basic block
                                            if (!createAndAddMetadata(duplicatedBasicBlock)) {
                                                throw new ArgumentException("Creating metadata for the duplicated basic block failed.");
                                            }

                                            break;

                                        }
                                    default: {

                                            // use a random possible basic block as target
                                            int randPosition = this.prng.Next(possibleTargetBasicBlocks.Count());
                                            BasicBlock newTarget = possibleTargetBasicBlocks.ElementAt(randPosition);

                                            target = new Target();
                                            target.basicBlock = newTarget;
                                            listOfTargets.Add(target);

                                            // set switch taken branch for current valid path id to the chosen branch target
                                            stateExitBranch.takenTarget[currentValidPathId] = target.basicBlock;
                                            target.basicBlock.entryBranches.Add(stateExitBranch);

                                            break;

                                        }

                                }

                            }

                            // set current valid path id of the target
                            target.currentValidPathId = currentValidPathId;

                            // get the link from the current basic block to the obfuscation graph
                            BasicBlockGraphNodeLink currentGraphLink = null;
                            foreach (BasicBlockGraphNodeLink tempLink in metadata.correspondingGraphNodes) {
                                if (tempLink.validPathId == currentValidPathId) {
                                    currentGraphLink = tempLink;
                                    break;
                                }
                            }
                            if (currentGraphLink == null) {
                                throw new ArgumentNullException("Could not find link from current basic block to the graph for the given path id.");
                            }

                            // get the metadata for the target basic block
                            GraphTransformerMetadataBasicBlock targetMetadata = null;
                            for (int i = 0; i < target.basicBlock.transformationMetadata.Count(); i++) {
                                // get graph transformer metadata for basic blocks
                                if ((target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock) == null) {
                                    continue;
                                }
                                targetMetadata = (target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock);
                                break;
                            }
                            if (targetMetadata == null) {
                                throw new ArgumentException("Not able to find metadata for target basic block.");
                            }

                            // get the link from the target basic block to the obfuscation graph
                            BasicBlockGraphNodeLink targetGraphLink = null;
                            foreach (BasicBlockGraphNodeLink tempLink in targetMetadata.correspondingGraphNodes) {
                                if (tempLink.validPathId == currentValidPathId) {
                                    targetGraphLink = tempLink;
                                    break;
                                }
                            }
                            if (targetGraphLink == null) {
                                throw new ArgumentNullException("Could not find link from target basic block to the graph for the given path id.");
                            }

                            // choose randomly to either just correct the pointer position or to inject a "state change"
                            switch (this.prng.Next(this.stateChangeWeight)) {

                                // case: "state change"
                                case 0:
                                case 1:
                                case 2:
                                case 3:
                                case 4:
                                case 5: {

                                        // generate code for the "state change"
                                        BasicBlock stateChangeBasicBlock = null;
                                        SwitchBranchTarget stateChangeExitBranch = null;
                                        this.createCodeStateChange(ref stateChangeBasicBlock, ref stateChangeExitBranch, randomStateGenerator, currentGraphLink, intStateLocal); 
                                        cfgManipulator.insertBasicBlockBetweenBranch(stateExitBranch, currentValidPathId, stateChangeBasicBlock, stateChangeExitBranch, 0);

                                        // create for each valid path a branch in the switch statement of the "state change"
                                        for (int changedCurrentValidPathId = 0; changedCurrentValidPathId < this.graph.graphValidPathCount; changedCurrentValidPathId++) {

                                            // ignore first valid path (with id 0)
                                            // => add artificial branch to original branch target (for the changed current valid path id)
                                            if (changedCurrentValidPathId != 0) {

                                                // fill switch taken branch list with null when index is out of range
                                                while (stateChangeExitBranch.takenTarget.Count() <= changedCurrentValidPathId) {
                                                    stateChangeExitBranch.takenTarget.Add(null);
                                                }

                                                // randomly chose what the target of the current branch should be (weight to use an existing basic block to have less memory consumption)
                                                switch (this.prng.Next(this.duplicateBasicBlockWeight)) {
                                                    case 0:
                                                    case 1:
                                                    case 2:
                                                    case 3:
                                                    case 4:
                                                    case 5: {

                                                            // duplicate the target basic block
                                                            BasicBlock duplicatedBasicBlock = this.duplicateBasicBlock(originalMethodCfg, methodCfg, listOfTargets.ElementAt(0).basicBlock);
                                                            methodCfg.basicBlocks.Add(duplicatedBasicBlock);
                                                            basicBlocksToProcess.Add(duplicatedBasicBlock);

                                                            // add new basic block as target of the switch case
                                                            stateChangeExitBranch.takenTarget[changedCurrentValidPathId] = duplicatedBasicBlock;
                                                            duplicatedBasicBlock.entryBranches.Add(stateChangeExitBranch);

                                                            // add new basic block to the list of target basic blocks
                                                            target = new Target();
                                                            target.basicBlock = duplicatedBasicBlock;
                                                            listOfTargets.Add(target);

                                                            // add duplicated basic block to the list of possible target basic blocks
                                                            possibleTargetBasicBlocks.Add(duplicatedBasicBlock);

                                                            // create metadata for the newly created basic block
                                                            if (!createAndAddMetadata(duplicatedBasicBlock)) {
                                                                throw new ArgumentException("Creating metadata for the duplicated basic block failed.");
                                                            }

                                                            break;

                                                        }
                                                    default: {

                                                            // use a random possible basic block as target
                                                            int randPosition = this.prng.Next(possibleTargetBasicBlocks.Count());
                                                            BasicBlock newTarget = possibleTargetBasicBlocks.ElementAt(randPosition);

                                                            target = new Target();
                                                            target.basicBlock = newTarget;
                                                            listOfTargets.Add(target);

                                                            // set switch taken branch for current valid path id to the chosen branch target
                                                            stateChangeExitBranch.takenTarget[changedCurrentValidPathId] = target.basicBlock;
                                                            target.basicBlock.entryBranches.Add(stateChangeExitBranch);

                                                            break;

                                                        }
                                                }

                                            }

                                            // set current valid path id of the target
                                            target.currentValidPathId = changedCurrentValidPathId;

                                            // get the metadata for the change target basic block
                                            GraphTransformerMetadataBasicBlock changeTargetMetadata = null;
                                            for (int i = 0; i < target.basicBlock.transformationMetadata.Count(); i++) {
                                                // get graph transformer metadata for basic blocks
                                                if ((target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock) == null) {
                                                    continue;
                                                }
                                                changeTargetMetadata = (target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock);
                                                break;
                                            }
                                            if (changeTargetMetadata == null) {
                                                throw new ArgumentException("Not able to find metadata for target basic block.");
                                            }

                                            // get the link from the change target basic block to the obfuscation graph
                                            BasicBlockGraphNodeLink changeTargetGraphLink = null;
                                            foreach (BasicBlockGraphNodeLink tempLink in changeTargetMetadata.correspondingGraphNodes) {
                                                if (tempLink.validPathId == changedCurrentValidPathId) {
                                                    changeTargetGraphLink = tempLink;
                                                    break;
                                                }
                                            }
                                            if (changeTargetGraphLink == null) {
                                                throw new ArgumentNullException("Could not find link from target basic block to the graph for the given path id.");
                                            }

                                            // correct the pointer position to the obfuscation graph
                                            this.injectLinkMoveCode(cfgManipulator, currentGraphLink, changeTargetGraphLink, stateChangeExitBranch, changedCurrentValidPathId, currentNodeLocal);

                                            // if debugging is activated => dump method cfg and add code to dump current graph
                                            if (this.debugging) {
                                                List<IOperation> debugOperations = new List<IOperation>();
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, this.debuggingNumber.ToString().PadLeft(3, '0') + "_" + target.basicBlock.id.ToString() + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method)));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.interfaceStartPointerGet));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.debuggingDumpGraphMethod));
                                                target.basicBlock.operations.InsertRange(0, debugOperations);

                                                //this.logger.dumpMethodCfg(methodCfg, this.debuggingNumber.ToString().PadLeft(3, '0') + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method));
                                                this.debuggingNumber++;
                                            }

                                        }

                                        break;
                                    }

                                // case: correct pointer position
                                default: {

                                        this.injectLinkMoveCode(cfgManipulator, currentGraphLink, targetGraphLink, stateExitBranch, currentGraphLink.validPathId, currentNodeLocal);

                                        // if debugging is activated => dump method cfg and add code to dump current graph
                                        if (this.debugging) {
                                            List<IOperation> debugOperations = new List<IOperation>();
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, this.debuggingNumber.ToString().PadLeft(3, '0') + "_" + target.basicBlock.id.ToString() + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method)));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.interfaceStartPointerGet));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.debuggingDumpGraphMethod));
                                            target.basicBlock.operations.InsertRange(0, debugOperations);

                                            //this.logger.dumpMethodCfg(methodCfg, this.debuggingNumber.ToString().PadLeft(3, '0') + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method));
                                            this.debuggingNumber++;
                                        }

                                        break;
                                    }
                            }
                        }

                    }

                    // check if the current exit branch is of type "unconditional branch target"
                    else if (currentBasicBlock.exitBranch as UnconditionalBranchTarget != null) {

                        UnconditionalBranchTarget introBranch = (UnconditionalBranchTarget)currentBasicBlock.exitBranch;

                        // search for all semantically equal basic blocks in the modified method CFG (as possible targets)
                        int searchSementicId = introBranch.takenTarget.semanticId;
                        List<BasicBlock> possibleTargetBasicBlocks = new List<BasicBlock>();
                        if (searchSementicId != -1) {
                            foreach (BasicBlock searchBasicBlock in methodCfg.basicBlocks) {
                                if (searchBasicBlock.semanticId == searchSementicId) {
                                    possibleTargetBasicBlocks.Add(searchBasicBlock);
                                }
                            }
                            if (possibleTargetBasicBlocks.Count() == 0) {
                                throw new ArgumentException("Not able to find any semantically equal basic block in CFG.");
                            }
                        }
                        else {
                            possibleTargetBasicBlocks.Add(introBranch.takenTarget);
                        }

                        // create a list of target basic blocks of this branch
                        List<Target> listOfTargets = new List<Target>();
                        Target target = new Target();
                        target.basicBlock = introBranch.takenTarget;
                        listOfTargets.Add(target);

                        // generate code for the "state switch"
                        BasicBlock stateBasicBlock = null;
                        SwitchBranchTarget stateExitBranch = null;
                        this.createCodeStateSwitch(ref stateBasicBlock, ref stateExitBranch, intStateLocal, metadata.correspondingGraphNodes);
                        cfgManipulator.insertBasicBlockBetweenBranch(introBranch, stateBasicBlock, stateExitBranch, 0);

                        // create for each valid path a branch in the switch statement of the "state switch"
                        for (currentValidPathId = 0; currentValidPathId < this.graph.graphValidPathCount; currentValidPathId++) {

                            // ignore first valid path (with id 0)
                            // => add artificial branch to original branch target (for the current valid path id)
                            if (currentValidPathId != 0) {

                                // fill switch taken branch list with null when index is out of range
                                while (stateExitBranch.takenTarget.Count() <= currentValidPathId) {
                                    stateExitBranch.takenTarget.Add(null);
                                }

                                // randomly chose what the target of the current branch should be (weight to use an existing basic block to have less memory consumption)
                                switch (this.prng.Next(this.duplicateBasicBlockWeight)) {
                                    case 0:
                                    case 1:
                                    case 2:
                                    case 3:
                                    case 4:
                                    case 5: {

                                            // duplicate the target basic block
                                            BasicBlock duplicatedBasicBlock = this.duplicateBasicBlock(originalMethodCfg, methodCfg, listOfTargets.ElementAt(0).basicBlock);
                                            methodCfg.basicBlocks.Add(duplicatedBasicBlock);
                                            basicBlocksToProcess.Add(duplicatedBasicBlock);

                                            // add new basic block as target of the switch case
                                            stateExitBranch.takenTarget[currentValidPathId] = duplicatedBasicBlock;
                                            duplicatedBasicBlock.entryBranches.Add(stateExitBranch);

                                            // add new basic block to the list of target basic blocks
                                            target = new Target();
                                            target.basicBlock = duplicatedBasicBlock;
                                            listOfTargets.Add(target);

                                            // add duplicated basic block to the list of possible target basic blocks
                                            possibleTargetBasicBlocks.Add(duplicatedBasicBlock);

                                            // create metadata for the newly created basic block
                                            if (!createAndAddMetadata(duplicatedBasicBlock)) {
                                                throw new ArgumentException("Creating metadata for the duplicated basic block failed.");
                                            }

                                            break;

                                        }
                                    default: {

                                            // use a random possible basic block as target
                                            int randPosition = this.prng.Next(possibleTargetBasicBlocks.Count());
                                            BasicBlock newTarget = possibleTargetBasicBlocks.ElementAt(randPosition);

                                            target = new Target();
                                            target.basicBlock = newTarget;
                                            listOfTargets.Add(target);

                                            //set switch taken branch for current valid path id to the chosen branch target
                                            stateExitBranch.takenTarget[currentValidPathId] = target.basicBlock;
                                            target.basicBlock.entryBranches.Add(stateExitBranch);

                                            break;

                                        }

                                }

                            }

                            // set current valid path id of the target
                            target.currentValidPathId = currentValidPathId;

                            // get the link from the current basic block to the obfuscation graph
                            BasicBlockGraphNodeLink currentGraphLink = null;
                            foreach (BasicBlockGraphNodeLink tempLink in metadata.correspondingGraphNodes) {
                                if (tempLink.validPathId == currentValidPathId) {
                                    currentGraphLink = tempLink;
                                    break;
                                }
                            }
                            if (currentGraphLink == null) {
                                throw new ArgumentNullException("Could not find link from current basic block to the graph for the given path id.");
                            }

                            // get the metadata for the target basic block
                            GraphTransformerMetadataBasicBlock targetMetadata = null;
                            for (int i = 0; i < target.basicBlock.transformationMetadata.Count(); i++) {
                                // get graph transformer metadata for basic blocks
                                if ((target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock) == null) {
                                    continue;
                                }
                                targetMetadata = (target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock);
                                break;
                            }
                            if (targetMetadata == null) {
                                throw new ArgumentException("Not able to find metadata for target basic block.");
                            }

                            // get the link from the target basic block to the obfuscation graph
                            BasicBlockGraphNodeLink targetGraphLink = null;
                            foreach (BasicBlockGraphNodeLink tempLink in targetMetadata.correspondingGraphNodes) {
                                if (tempLink.validPathId == currentValidPathId) {
                                    targetGraphLink = tempLink;
                                    break;
                                }
                            }
                            if (targetGraphLink == null) {
                                throw new ArgumentNullException("Could not find link from target basic block to the graph for the given path id.");
                            }

                            // choose randomly to either just correct the pointer position or to inject a "state change"
                            switch (this.prng.Next(this.stateChangeWeight)) {

                                // case: "state change"
                                case 0:
                                case 1:
                                case 2:
                                case 3:
                                case 4:
                                case 5: {

                                        // generate code for the "state change"
                                        BasicBlock stateChangeBasicBlock = null;
                                        SwitchBranchTarget stateChangeExitBranch = null;
                                        this.createCodeStateChange(ref stateChangeBasicBlock, ref stateChangeExitBranch, randomStateGenerator, currentGraphLink, intStateLocal);
                                        cfgManipulator.insertBasicBlockBetweenBranch(stateExitBranch, currentValidPathId, stateChangeBasicBlock, stateChangeExitBranch, 0);

                                        // create for each valid path a branch in the switch statement of the "state change"
                                        for (int changedCurrentValidPathId = 0; changedCurrentValidPathId < this.graph.graphValidPathCount; changedCurrentValidPathId++) {

                                            // ignore first valid path (with id 0)
                                            // => add artificial branch to original branch target (for the changed current valid path id)
                                            if (changedCurrentValidPathId != 0) {

                                                // fill switch taken branch list with null when index is out of range
                                                while (stateChangeExitBranch.takenTarget.Count() <= changedCurrentValidPathId) {
                                                    stateChangeExitBranch.takenTarget.Add(null);
                                                }

                                                // randomly chose what the target of the current branch should be (weight to use an existing basic block to have less memory consumption)
                                                switch (this.prng.Next(this.duplicateBasicBlockWeight)) {
                                                    case 0:
                                                    case 1:
                                                    case 2:
                                                    case 3:
                                                    case 4:
                                                    case 5: {

                                                            // duplicate the target basic block
                                                            BasicBlock duplicatedBasicBlock = this.duplicateBasicBlock(originalMethodCfg, methodCfg, listOfTargets.ElementAt(0).basicBlock);
                                                            methodCfg.basicBlocks.Add(duplicatedBasicBlock);
                                                            basicBlocksToProcess.Add(duplicatedBasicBlock);

                                                            // add new basic block as target of the switch case
                                                            stateChangeExitBranch.takenTarget[changedCurrentValidPathId] = duplicatedBasicBlock;
                                                            duplicatedBasicBlock.entryBranches.Add(stateChangeExitBranch);

                                                            // add new basic block to the list of target basic blocks
                                                            target = new Target();
                                                            target.basicBlock = duplicatedBasicBlock;
                                                            listOfTargets.Add(target);

                                                            // add duplicated basic block to the list of possible target basic blocks
                                                            possibleTargetBasicBlocks.Add(duplicatedBasicBlock);

                                                            // create metadata for the newly created basic block
                                                            if (!createAndAddMetadata(duplicatedBasicBlock)) {
                                                                throw new ArgumentException("Creating metadata for the duplicated basic block failed.");
                                                            }

                                                            break;

                                                        }
                                                    default: {

                                                            // use a random possible basic block as target
                                                            int randPosition = this.prng.Next(possibleTargetBasicBlocks.Count());
                                                            BasicBlock newTarget = possibleTargetBasicBlocks.ElementAt(randPosition);

                                                            target = new Target();
                                                            target.basicBlock = newTarget;
                                                            listOfTargets.Add(target);

                                                            // set switch taken branch for current valid path id to the chosen branch target
                                                            stateChangeExitBranch.takenTarget[changedCurrentValidPathId] = target.basicBlock;
                                                            target.basicBlock.entryBranches.Add(stateChangeExitBranch);

                                                            break;

                                                        }
                                                }

                                            }

                                            // set current valid path id of the target
                                            target.currentValidPathId = changedCurrentValidPathId;

                                            // get the metadata for the change target basic block
                                            GraphTransformerMetadataBasicBlock changeTargetMetadata = null;
                                            for (int i = 0; i < target.basicBlock.transformationMetadata.Count(); i++) {
                                                // get graph transformer metadata for basic blocks
                                                if ((target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock) == null) {
                                                    continue;
                                                }
                                                changeTargetMetadata = (target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock);
                                                break;
                                            }
                                            if (changeTargetMetadata == null) {
                                                throw new ArgumentException("Not able to find metadata for target basic block.");
                                            }

                                            // get the link from the change target basic block to the obfuscation graph
                                            BasicBlockGraphNodeLink changeTargetGraphLink = null;
                                            foreach (BasicBlockGraphNodeLink tempLink in changeTargetMetadata.correspondingGraphNodes) {
                                                if (tempLink.validPathId == changedCurrentValidPathId) {
                                                    changeTargetGraphLink = tempLink;
                                                    break;
                                                }
                                            }
                                            if (changeTargetGraphLink == null) {
                                                throw new ArgumentNullException("Could not find link from target basic block to the graph for the given path id.");
                                            }

                                            // correct the pointer position to the obfuscation graph
                                            this.injectLinkMoveCode(cfgManipulator, currentGraphLink, changeTargetGraphLink, stateChangeExitBranch, changedCurrentValidPathId, currentNodeLocal);

                                            // if debugging is activated => dump method cfg and add code to dump current graph
                                            if (this.debugging) {
                                                List<IOperation> debugOperations = new List<IOperation>();
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, this.debuggingNumber.ToString().PadLeft(3, '0') + "_" + target.basicBlock.id.ToString() + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method)));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.interfaceStartPointerGet));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.debuggingDumpGraphMethod));
                                                target.basicBlock.operations.InsertRange(0, debugOperations);

                                                //this.logger.dumpMethodCfg(methodCfg, this.debuggingNumber.ToString().PadLeft(3, '0') + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method));
                                                this.debuggingNumber++;
                                            }

                                        }

                                        break;
                                    }

                                // case: correct pointer position
                                default: {

                                        this.injectLinkMoveCode(cfgManipulator, currentGraphLink, targetGraphLink, stateExitBranch, currentGraphLink.validPathId, currentNodeLocal);

                                        // if debugging is activated => dump method cfg and add code to dump current graph
                                        if (this.debugging) {
                                            List<IOperation> debugOperations = new List<IOperation>();
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, this.debuggingNumber.ToString().PadLeft(3, '0') + "_" + target.basicBlock.id.ToString() + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method)));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.interfaceStartPointerGet));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.debuggingDumpGraphMethod));
                                            target.basicBlock.operations.InsertRange(0, debugOperations);

                                            //this.logger.dumpMethodCfg(methodCfg, this.debuggingNumber.ToString().PadLeft(3, '0') + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method));
                                            this.debuggingNumber++;
                                        }

                                        break;
                                    }
                            }
                        }

                    }

                    // check if the current exit branch is of type "conditional branch target"
                    else if (currentBasicBlock.exitBranch as ConditionalBranchTarget != null) {

                        ConditionalBranchTarget introBranch = (ConditionalBranchTarget)currentBasicBlock.exitBranch;

                        // search for all semantically equal basic blocks in the modified method CFG (as possible targets)
                        int searchSementicId = introBranch.takenTarget.semanticId;
                        List<BasicBlock> possibleTargetBasicBlocks = new List<BasicBlock>();
                        if (searchSementicId != -1) {
                            foreach (BasicBlock searchBasicBlock in methodCfg.basicBlocks) {
                                if (searchBasicBlock.semanticId == searchSementicId) {
                                    possibleTargetBasicBlocks.Add(searchBasicBlock);
                                }
                            }
                            if (possibleTargetBasicBlocks.Count() == 0) {
                                throw new ArgumentException("Not able to find any semantically equal basic block in CFG.");
                            }
                        }
                        else {
                            possibleTargetBasicBlocks.Add(introBranch.takenTarget);
                        }

                        // create a list of target basic blocks of this branch
                        List<Target> listOfTargets = new List<Target>();
                        Target target = new Target();
                        target.basicBlock = introBranch.takenTarget;
                        listOfTargets.Add(target);

                        // generate code for the "state switch"
                        BasicBlock stateBasicBlock = null;
                        SwitchBranchTarget stateExitBranch = null;
                        this.createCodeStateSwitch(ref stateBasicBlock, ref stateExitBranch, intStateLocal, metadata.correspondingGraphNodes);
                        cfgManipulator.insertBasicBlockBetweenBranch(introBranch, true, stateBasicBlock, stateExitBranch, 0);

                        // create for each valid path a branch in the switch statement of the "state switch"
                        for (currentValidPathId = 0; currentValidPathId < this.graph.graphValidPathCount; currentValidPathId++) {

                            // ignore first valid path (with id 0)
                            // => add artificial branch to original branch target (for the current valid path id)
                            if (currentValidPathId != 0) {

                                // fill switch taken branch list with null when index is out of range
                                while (stateExitBranch.takenTarget.Count() <= currentValidPathId) {
                                    stateExitBranch.takenTarget.Add(null);
                                }

                                // randomly chose what the target of the current branch should be (weight to use an existing basic block to have less memory consumption)
                                switch (this.prng.Next(this.duplicateBasicBlockWeight)) {
                                    case 0:
                                    case 1:
                                    case 2:
                                    case 3:
                                    case 4:
                                    case 5: {

                                            // duplicate the target basic block
                                            BasicBlock duplicatedBasicBlock = this.duplicateBasicBlock(originalMethodCfg, methodCfg, listOfTargets.ElementAt(0).basicBlock);
                                            methodCfg.basicBlocks.Add(duplicatedBasicBlock);
                                            basicBlocksToProcess.Add(duplicatedBasicBlock);

                                            // add new basic block as target of the switch case
                                            stateExitBranch.takenTarget[currentValidPathId] = duplicatedBasicBlock;
                                            duplicatedBasicBlock.entryBranches.Add(stateExitBranch);

                                            // add new basic block to the list of target basic blocks
                                            target = new Target();
                                            target.basicBlock = duplicatedBasicBlock;
                                            listOfTargets.Add(target);

                                            // add duplicated basic block to the list of possible target basic blocks
                                            possibleTargetBasicBlocks.Add(duplicatedBasicBlock);

                                            // create metadata for the newly created basic block
                                            if (!createAndAddMetadata(duplicatedBasicBlock)) {
                                                throw new ArgumentException("Creating metadata for the duplicated basic block failed.");
                                            }

                                            break;

                                        }
                                    default: {

                                            // use a random possible basic block as target
                                            int randPosition = this.prng.Next(possibleTargetBasicBlocks.Count());
                                            BasicBlock newTarget = possibleTargetBasicBlocks.ElementAt(randPosition);

                                            target = new Target();
                                            target.basicBlock = newTarget;
                                            listOfTargets.Add(target);

                                            //set switch taken branch for current valid path id to the chosen branch target
                                            stateExitBranch.takenTarget[currentValidPathId] = target.basicBlock;
                                            target.basicBlock.entryBranches.Add(stateExitBranch);

                                            break;

                                        }

                                }

                            }

                            // set current valid path id of the target
                            target.currentValidPathId = currentValidPathId;

                            // get the link from the current basic block to the obfuscation graph
                            BasicBlockGraphNodeLink currentGraphLink = null;
                            foreach (BasicBlockGraphNodeLink tempLink in metadata.correspondingGraphNodes) {
                                if (tempLink.validPathId == currentValidPathId) {
                                    currentGraphLink = tempLink;
                                    break;
                                }
                            }
                            if (currentGraphLink == null) {
                                throw new ArgumentNullException("Could not find link from current basic block to the graph for the given path id.");
                            }

                            // get the metadata for the target basic block
                            GraphTransformerMetadataBasicBlock targetMetadata = null;
                            for (int i = 0; i < target.basicBlock.transformationMetadata.Count(); i++) {
                                // get graph transformer metadata for basic blocks
                                if ((target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock) == null) {
                                    continue;
                                }
                                targetMetadata = (target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock);
                                break;
                            }
                            if (targetMetadata == null) {
                                throw new ArgumentException("Not able to find metadata for target basic block.");
                            }

                            // get the link from the target basic block to the obfuscation graph
                            BasicBlockGraphNodeLink targetGraphLink = null;
                            foreach (BasicBlockGraphNodeLink tempLink in targetMetadata.correspondingGraphNodes) {
                                if (tempLink.validPathId == currentValidPathId) {
                                    targetGraphLink = tempLink;
                                    break;
                                }
                            }
                            if (targetGraphLink == null) {
                                throw new ArgumentNullException("Could not find link from target basic block to the graph for the given path id.");
                            }

                            // choose randomly to either just correct the pointer position or to inject a "state change"
                            switch (this.prng.Next(this.stateChangeWeight)) {

                                // case: "state change"
                                case 0:
                                case 1:
                                case 2:
                                case 3:
                                case 4:
                                case 5: {

                                        // generate code for the "state change"
                                        BasicBlock stateChangeBasicBlock = null;
                                        SwitchBranchTarget stateChangeExitBranch = null;
                                        this.createCodeStateChange(ref stateChangeBasicBlock, ref stateChangeExitBranch, randomStateGenerator, currentGraphLink, intStateLocal);
                                        cfgManipulator.insertBasicBlockBetweenBranch(stateExitBranch, currentValidPathId, stateChangeBasicBlock, stateChangeExitBranch, 0);

                                        // create for each valid path a branch in the switch statement of the "state change"
                                        for (int changedCurrentValidPathId = 0; changedCurrentValidPathId < this.graph.graphValidPathCount; changedCurrentValidPathId++) {

                                            // ignore first valid path (with id 0)
                                            // => add artificial branch to original branch target (for the changed current valid path id)
                                            if (changedCurrentValidPathId != 0) {

                                                // fill switch taken branch list with null when index is out of range
                                                while (stateChangeExitBranch.takenTarget.Count() <= changedCurrentValidPathId) {
                                                    stateChangeExitBranch.takenTarget.Add(null);
                                                }

                                                // randomly chose what the target of the current branch should be (weight to use an existing basic block to have less memory consumption)
                                                switch (this.prng.Next(this.duplicateBasicBlockWeight)) {
                                                    case 0:
                                                    case 1:
                                                    case 2:
                                                    case 3:
                                                    case 4:
                                                    case 5: {

                                                            // duplicate the target basic block
                                                            BasicBlock duplicatedBasicBlock = this.duplicateBasicBlock(originalMethodCfg, methodCfg, listOfTargets.ElementAt(0).basicBlock);
                                                            methodCfg.basicBlocks.Add(duplicatedBasicBlock);
                                                            basicBlocksToProcess.Add(duplicatedBasicBlock);

                                                            // add new basic block as target of the switch case
                                                            stateChangeExitBranch.takenTarget[changedCurrentValidPathId] = duplicatedBasicBlock;
                                                            duplicatedBasicBlock.entryBranches.Add(stateChangeExitBranch);

                                                            // add new basic block to the list of target basic blocks
                                                            target = new Target();
                                                            target.basicBlock = duplicatedBasicBlock;
                                                            listOfTargets.Add(target);

                                                            // add duplicated basic block to the list of possible target basic blocks
                                                            possibleTargetBasicBlocks.Add(duplicatedBasicBlock);

                                                            // create metadata for the newly created basic block
                                                            if (!createAndAddMetadata(duplicatedBasicBlock)) {
                                                                throw new ArgumentException("Creating metadata for the duplicated basic block failed.");
                                                            }

                                                            break;

                                                        }
                                                    default: {

                                                            // use a random possible basic block as target
                                                            int randPosition = this.prng.Next(possibleTargetBasicBlocks.Count());
                                                            BasicBlock newTarget = possibleTargetBasicBlocks.ElementAt(randPosition);

                                                            target = new Target();
                                                            target.basicBlock = newTarget;
                                                            listOfTargets.Add(target);

                                                            // set switch taken branch for current valid path id to the chosen branch target
                                                            stateChangeExitBranch.takenTarget[changedCurrentValidPathId] = target.basicBlock;
                                                            target.basicBlock.entryBranches.Add(stateChangeExitBranch);

                                                            break;

                                                        }
                                                }

                                            }

                                            // set current valid path id of the target
                                            target.currentValidPathId = changedCurrentValidPathId;

                                            // get the metadata for the change target basic block
                                            GraphTransformerMetadataBasicBlock changeTargetMetadata = null;
                                            for (int i = 0; i < target.basicBlock.transformationMetadata.Count(); i++) {
                                                // get graph transformer metadata for basic blocks
                                                if ((target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock) == null) {
                                                    continue;
                                                }
                                                changeTargetMetadata = (target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock);
                                                break;
                                            }
                                            if (changeTargetMetadata == null) {
                                                throw new ArgumentException("Not able to find metadata for target basic block.");
                                            }

                                            // get the link from the change target basic block to the obfuscation graph
                                            BasicBlockGraphNodeLink changeTargetGraphLink = null;
                                            foreach (BasicBlockGraphNodeLink tempLink in changeTargetMetadata.correspondingGraphNodes) {
                                                if (tempLink.validPathId == changedCurrentValidPathId) {
                                                    changeTargetGraphLink = tempLink;
                                                    break;
                                                }
                                            }
                                            if (changeTargetGraphLink == null) {
                                                throw new ArgumentNullException("Could not find link from target basic block to the graph for the given path id.");
                                            }

                                            // correct the pointer position to the obfuscation graph
                                            this.injectLinkMoveCode(cfgManipulator, currentGraphLink, changeTargetGraphLink, stateChangeExitBranch, changedCurrentValidPathId, currentNodeLocal);

                                            // if debugging is activated => dump method cfg and add code to dump current graph
                                            if (this.debugging) {
                                                List<IOperation> debugOperations = new List<IOperation>();
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, this.debuggingNumber.ToString().PadLeft(3, '0') + "_" + target.basicBlock.id.ToString() + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method)));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.interfaceStartPointerGet));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.debuggingDumpGraphMethod));
                                                target.basicBlock.operations.InsertRange(0, debugOperations);

                                                //this.logger.dumpMethodCfg(methodCfg, this.debuggingNumber.ToString().PadLeft(3, '0') + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method));
                                                this.debuggingNumber++;
                                            }

                                        }

                                        break;
                                    }

                                // case: correct pointer position
                                default: {

                                        this.injectLinkMoveCode(cfgManipulator, currentGraphLink, targetGraphLink, stateExitBranch, currentGraphLink.validPathId, currentNodeLocal);

                                        // if debugging is activated => dump method cfg and add code to dump current graph
                                        if (this.debugging) {
                                            List<IOperation> debugOperations = new List<IOperation>();
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, this.debuggingNumber.ToString().PadLeft(3, '0') + "_" + target.basicBlock.id.ToString() + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method)));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.interfaceStartPointerGet));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.debuggingDumpGraphMethod));
                                            target.basicBlock.operations.InsertRange(0, debugOperations);

                                            //this.logger.dumpMethodCfg(methodCfg, this.debuggingNumber.ToString().PadLeft(3, '0') + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method));
                                            this.debuggingNumber++;
                                        }

                                        break;
                                    }
                            }
                        }

                        // search for all semantically equal basic blocks in the modified method CFG (as possible targets)
                        searchSementicId = introBranch.notTakenTarget.semanticId;
                        possibleTargetBasicBlocks = new List<BasicBlock>();
                        if (searchSementicId != -1) {
                            foreach (BasicBlock searchBasicBlock in methodCfg.basicBlocks) {
                                if (searchBasicBlock.semanticId == searchSementicId) {
                                    possibleTargetBasicBlocks.Add(searchBasicBlock);
                                }
                            }
                            if (possibleTargetBasicBlocks.Count() == 0) {
                                throw new ArgumentException("Not able to find any semantically equal basic block in CFG.");
                            }
                        }
                        else {
                            possibleTargetBasicBlocks.Add(introBranch.notTakenTarget);
                        }

                        // create a list of target basic blocks of this branch
                        listOfTargets = new List<Target>();
                        target = new Target();
                        target.basicBlock = introBranch.notTakenTarget;
                        listOfTargets.Add(target);

                        // generate code for the "state switch"
                        stateBasicBlock = null;
                        stateExitBranch = null;
                        this.createCodeStateSwitch(ref stateBasicBlock, ref stateExitBranch, intStateLocal, metadata.correspondingGraphNodes);
                        cfgManipulator.insertBasicBlockBetweenBranch(introBranch, false, stateBasicBlock, stateExitBranch, 0);

                        // create for each valid path a branch in the switch statement of the "state switch"
                        for (currentValidPathId = 0; currentValidPathId < this.graph.graphValidPathCount; currentValidPathId++) {

                            // ignore first valid path (with id 0)
                            // => add artificial branch to original branch target (for the current valid path id)
                            if (currentValidPathId != 0) {

                                // fill switch taken branch list with null when index is out of range
                                while (stateExitBranch.takenTarget.Count() <= currentValidPathId) {
                                    stateExitBranch.takenTarget.Add(null);
                                }

                                // randomly chose what the target of the current branch should be (weight to use an existing basic block to have less memory consumption)
                                switch (this.prng.Next(this.duplicateBasicBlockWeight)) {
                                    case 0:
                                    case 1:
                                    case 2:
                                    case 3:
                                    case 4:
                                    case 5: {

                                            // duplicate the target basic block
                                            BasicBlock duplicatedBasicBlock = this.duplicateBasicBlock(originalMethodCfg, methodCfg, listOfTargets.ElementAt(0).basicBlock);
                                            methodCfg.basicBlocks.Add(duplicatedBasicBlock);
                                            basicBlocksToProcess.Add(duplicatedBasicBlock);

                                            // add new basic block as target of the switch case
                                            stateExitBranch.takenTarget[currentValidPathId] = duplicatedBasicBlock;
                                            duplicatedBasicBlock.entryBranches.Add(stateExitBranch);

                                            // add new basic block to the list of target basic blocks
                                            target = new Target();
                                            target.basicBlock = duplicatedBasicBlock;
                                            listOfTargets.Add(target);

                                            // add duplicated basic block to the list of possible target basic blocks
                                            possibleTargetBasicBlocks.Add(duplicatedBasicBlock);

                                            // create metadata for the newly created basic block
                                            if (!createAndAddMetadata(duplicatedBasicBlock)) {
                                                throw new ArgumentException("Creating metadata for the duplicated basic block failed.");
                                            }

                                            break;

                                        }
                                    default: {

                                            // use a random possible basic block as target
                                            int randPosition = this.prng.Next(possibleTargetBasicBlocks.Count());
                                            BasicBlock newTarget = possibleTargetBasicBlocks.ElementAt(randPosition);

                                            target = new Target();
                                            target.basicBlock = newTarget;
                                            listOfTargets.Add(target);

                                            //set switch taken branch for current valid path id to the chosen branch target
                                            stateExitBranch.takenTarget[currentValidPathId] = target.basicBlock;
                                            target.basicBlock.entryBranches.Add(stateExitBranch);

                                            break;

                                        }

                                }

                            }

                            // set current valid path id of the target
                            target.currentValidPathId = currentValidPathId;

                            // get the link from the current basic block to the obfuscation graph
                            BasicBlockGraphNodeLink currentGraphLink = null;
                            foreach (BasicBlockGraphNodeLink tempLink in metadata.correspondingGraphNodes) {
                                if (tempLink.validPathId == currentValidPathId) {
                                    currentGraphLink = tempLink;
                                    break;
                                }
                            }
                            if (currentGraphLink == null) {
                                throw new ArgumentNullException("Could not find link from current basic block to the graph for the given path id.");
                            }

                            // get the metadata for the target basic block
                            GraphTransformerMetadataBasicBlock targetMetadata = null;
                            for (int i = 0; i < target.basicBlock.transformationMetadata.Count(); i++) {
                                // get graph transformer metadata for basic blocks
                                if ((target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock) == null) {
                                    continue;
                                }
                                targetMetadata = (target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock);
                                break;
                            }
                            if (targetMetadata == null) {
                                throw new ArgumentException("Not able to find metadata for target basic block.");
                            }

                            // get the link from the target basic block to the obfuscation graph
                            BasicBlockGraphNodeLink targetGraphLink = null;
                            foreach (BasicBlockGraphNodeLink tempLink in targetMetadata.correspondingGraphNodes) {
                                if (tempLink.validPathId == currentValidPathId) {
                                    targetGraphLink = tempLink;
                                    break;
                                }
                            }
                            if (targetGraphLink == null) {
                                throw new ArgumentNullException("Could not find link from target basic block to the graph for the given path id.");
                            }

                            // choose randomly to either just correct the pointer position or to inject a "state change"
                            switch (this.prng.Next(this.stateChangeWeight)) {

                                // case: "state change"
                                case 0:
                                case 1:
                                case 2:
                                case 3:
                                case 4:
                                case 5: {

                                        // generate code for the "state change"
                                        BasicBlock stateChangeBasicBlock = null;
                                        SwitchBranchTarget stateChangeExitBranch = null;
                                        this.createCodeStateChange(ref stateChangeBasicBlock, ref stateChangeExitBranch, randomStateGenerator, currentGraphLink, intStateLocal);
                                        cfgManipulator.insertBasicBlockBetweenBranch(stateExitBranch, currentValidPathId, stateChangeBasicBlock, stateChangeExitBranch, 0);

                                        // create for each valid path a branch in the switch statement of the "state change"
                                        for (int changedCurrentValidPathId = 0; changedCurrentValidPathId < this.graph.graphValidPathCount; changedCurrentValidPathId++) {

                                            // ignore first valid path (with id 0)
                                            // => add artificial branch to original branch target (for the changed current valid path id)
                                            if (changedCurrentValidPathId != 0) {

                                                // fill switch taken branch list with null when index is out of range
                                                while (stateChangeExitBranch.takenTarget.Count() <= changedCurrentValidPathId) {
                                                    stateChangeExitBranch.takenTarget.Add(null);
                                                }

                                                // randomly chose what the target of the current branch should be (weight to use an existing basic block to have less memory consumption)
                                                switch (this.prng.Next(this.duplicateBasicBlockWeight)) {
                                                    case 0:
                                                    case 1:
                                                    case 2:
                                                    case 3:
                                                    case 4:
                                                    case 5: {

                                                            // duplicate the target basic block
                                                            BasicBlock duplicatedBasicBlock = this.duplicateBasicBlock(originalMethodCfg, methodCfg, listOfTargets.ElementAt(0).basicBlock);
                                                            methodCfg.basicBlocks.Add(duplicatedBasicBlock);
                                                            basicBlocksToProcess.Add(duplicatedBasicBlock);

                                                            // add new basic block as target of the switch case
                                                            stateChangeExitBranch.takenTarget[changedCurrentValidPathId] = duplicatedBasicBlock;
                                                            duplicatedBasicBlock.entryBranches.Add(stateChangeExitBranch);

                                                            // add new basic block to the list of target basic blocks
                                                            target = new Target();
                                                            target.basicBlock = duplicatedBasicBlock;
                                                            listOfTargets.Add(target);

                                                            // add duplicated basic block to the list of possible target basic blocks
                                                            possibleTargetBasicBlocks.Add(duplicatedBasicBlock);

                                                            // create metadata for the newly created basic block
                                                            if (!createAndAddMetadata(duplicatedBasicBlock)) {
                                                                throw new ArgumentException("Creating metadata for the duplicated basic block failed.");
                                                            }

                                                            break;

                                                        }
                                                    default: {

                                                            // use a random possible basic block as target
                                                            int randPosition = this.prng.Next(possibleTargetBasicBlocks.Count());
                                                            BasicBlock newTarget = possibleTargetBasicBlocks.ElementAt(randPosition);

                                                            target = new Target();
                                                            target.basicBlock = newTarget;
                                                            listOfTargets.Add(target);

                                                            // set switch taken branch for current valid path id to the chosen branch target
                                                            stateChangeExitBranch.takenTarget[changedCurrentValidPathId] = target.basicBlock;
                                                            target.basicBlock.entryBranches.Add(stateChangeExitBranch);

                                                            break;

                                                        }
                                                }

                                            }

                                            // set current valid path id of the target
                                            target.currentValidPathId = changedCurrentValidPathId;

                                            // get the metadata for the change target basic block
                                            GraphTransformerMetadataBasicBlock changeTargetMetadata = null;
                                            for (int i = 0; i < target.basicBlock.transformationMetadata.Count(); i++) {
                                                // get graph transformer metadata for basic blocks
                                                if ((target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock) == null) {
                                                    continue;
                                                }
                                                changeTargetMetadata = (target.basicBlock.transformationMetadata.ElementAt(i) as GraphTransformerMetadataBasicBlock);
                                                break;
                                            }
                                            if (changeTargetMetadata == null) {
                                                throw new ArgumentException("Not able to find metadata for target basic block.");
                                            }

                                            // get the link from the change target basic block to the obfuscation graph
                                            BasicBlockGraphNodeLink changeTargetGraphLink = null;
                                            foreach (BasicBlockGraphNodeLink tempLink in changeTargetMetadata.correspondingGraphNodes) {
                                                if (tempLink.validPathId == changedCurrentValidPathId) {
                                                    changeTargetGraphLink = tempLink;
                                                    break;
                                                }
                                            }
                                            if (changeTargetGraphLink == null) {
                                                throw new ArgumentNullException("Could not find link from target basic block to the graph for the given path id.");
                                            }

                                            // correct the pointer position to the obfuscation graph
                                            this.injectLinkMoveCode(cfgManipulator, currentGraphLink, changeTargetGraphLink, stateChangeExitBranch, changedCurrentValidPathId, currentNodeLocal);

                                            // if debugging is activated => dump method cfg and add code to dump current graph
                                            if (this.debugging) {
                                                List<IOperation> debugOperations = new List<IOperation>();
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, this.debuggingNumber.ToString().PadLeft(3, '0') + "_" + target.basicBlock.id.ToString() + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method)));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.interfaceStartPointerGet));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));
                                                debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.debuggingDumpGraphMethod));
                                                target.basicBlock.operations.InsertRange(0, debugOperations);

                                                //this.logger.dumpMethodCfg(methodCfg, this.debuggingNumber.ToString().PadLeft(3, '0') + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method));
                                                this.debuggingNumber++;
                                            }

                                        }

                                        break;
                                    }

                                // case: correct pointer position
                                default: {

                                        this.injectLinkMoveCode(cfgManipulator, currentGraphLink, targetGraphLink, stateExitBranch, currentGraphLink.validPathId, currentNodeLocal);

                                        // if debugging is activated => dump method cfg and add code to dump current graph
                                        if (this.debugging) {
                                            List<IOperation> debugOperations = new List<IOperation>();
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldstr, this.debuggingNumber.ToString().PadLeft(3, '0') + "_" + target.basicBlock.id.ToString() + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method)));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldarg_0));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.interfaceStartPointerGet));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Ldloc, currentNodeLocal));
                                            debugOperations.Add(this.helperClass.createNewOperation(OperationCode.Callvirt, this.debuggingDumpGraphMethod));
                                            target.basicBlock.operations.InsertRange(0, debugOperations);

                                            //this.logger.dumpMethodCfg(methodCfg, this.debuggingNumber.ToString().PadLeft(3, '0') + "_obfu_" + this.logger.makeFuncSigString(methodCfg.method));
                                            this.debuggingNumber++;
                                        }

                                        break;
                                    }
                            }
                        }
                    }

                    // check if the current exit branch is of type "try block target"
                    else if (currentBasicBlock.exitBranch as TryBlockTarget != null) {
                        throw new ArgumentException("Not yet implemented.");
                    }

                    // check if the current exit branch is of type "switch branch target"
                    else if (currentBasicBlock.exitBranch as SwitchBranchTarget != null) {
                        throw new ArgumentException("Not yet implemented.");
                    }

                    // check if the current exit branch is of type "exception branch target"
                    else if (currentBasicBlock.exitBranch as ExceptionBranchTarget != null) {
                        throw new ArgumentException("Not yet implemented.");
                    }

                    // check if the current exit branch is of type "exit branch target"
                    else if (currentBasicBlock.exitBranch as ExitBranchTarget != null) {
                        continue;
                    }

                    // check if the current exit branch is of type "throw branch target"
                    else if (currentBasicBlock.exitBranch as ThrowBranchTarget != null) {
                        continue;
                    }

                    // this case should never be reached
                    else {
                        throw new ArgumentException("Do not know how to handle branch.");
                    }

                }

                System.Console.WriteLine("Basic Blocks to process (Control Flow): " + basicBlocksToProcess.Count());

                // check if basic blocks exist to process => if not break loop
                if (basicBlocksToProcess.Count() == 0) {
                    break;
                }

                // adjust probability to duplicate basic blocks (each processing round the probability drops to duplicate a basic block)
                this.duplicateBasicBlockWeight += this.duplicateBasicBlockCorrectionValue;

                // adjust probability to insert a state change (each processing round the probability drops to insert a state change)
                this.stateChangeWeight += this.stateChangeCorrectionValue;

            }

        }


        private void addOpaquePredicatesIter(CfgManipulator cfgManipulator, GraphRandomStateGenerator randomStateGenerator, MethodCfg originalMethodCfg, MethodCfg methodCfg, BasicBlock startBasicBlock, LocalDefinition intStateLocal, LocalDefinition currentNodeLocal) {

            // create a list of basic blocks that still have to be processed by the algorithm
            List<BasicBlock> basicBlocksToProcess = new List<BasicBlock>(methodCfg.basicBlocks);

            System.Console.WriteLine("Basic Blocks to process (Opaque Predicates): " + basicBlocksToProcess.Count());

            while (true) {

                List<BasicBlock> copiedList = new List<BasicBlock>(basicBlocksToProcess);
                foreach (BasicBlock currentBasicBlock in copiedList) {

                    // process all entry branches of the current basic block
                    List<IBranchTarget> copiedBranchList = new List<IBranchTarget>(currentBasicBlock.entryBranches);
                    foreach (IBranchTarget entryBranch in copiedBranchList) {

                        // get the metadata for the source basic block of the entry branch
                        BasicBlock sourceBasicBlock = entryBranch.sourceBasicBlock;
                        IGraphTransformerMetadata sourceMetadata = null;
                        for (int i = 0; i < sourceBasicBlock.transformationMetadata.Count(); i++) {
                            // get graph transformer metadata for basic blocks
                            if ((sourceBasicBlock.transformationMetadata.ElementAt(i) as IGraphTransformerMetadata) == null) {
                                continue;
                            }
                            sourceMetadata = (sourceBasicBlock.transformationMetadata.ElementAt(i) as IGraphTransformerMetadata);
                            break;
                        }
                        if (sourceMetadata == null) {
                            throw new ArgumentException("Not able to find metadata for source basic block of the entry branch.");
                        }

                        // only process entry branches with only one link to the obfuscation graph
                        if (sourceMetadata.correspondingGraphNodes.Count() != 1) {
                            continue;
                        }

                        // check if the source basic block is a basic block that moves the pointer to the obfuscation graph
                        // => use the link from the current basic block to the obfuscation graph
                        BasicBlockGraphNodeLink link = null;
                        if ((sourceMetadata as GraphTransformerNextNodeBasicBlock) != null) {

                            // get the metadata for the current basic block (because the source basic block has moved the pointer to the obfuscation graph)
                            IGraphTransformerMetadata currentMetadata = null;
                            for (int i = 0; i < currentBasicBlock.transformationMetadata.Count(); i++) {
                                // get graph transformer metadata for basic blocks
                                if ((currentBasicBlock.transformationMetadata.ElementAt(i) as IGraphTransformerMetadata) == null) {
                                    continue;
                                }
                                currentMetadata = (currentBasicBlock.transformationMetadata.ElementAt(i) as IGraphTransformerMetadata);
                                break;
                            }
                            if (currentMetadata == null) {
                                throw new ArgumentException("Not able to find metadata for source basic block of the entry branch.");
                            }

                            // when current basic block also moves the pointer to the obfuscation graph => skip opaque predicate insertion it (for the moment)
                            // (problem with obfuscation graph nodes that do not reside on the vpath because of pointer correction code)
                            if ((currentMetadata as GraphTransformerNextNodeBasicBlock) != null) {
                                continue;
                            }

                            // get the link to the correct obfuscation graph node for the valid path that goes through the source basic block
                            BasicBlockGraphNodeLink sourceLink = sourceMetadata.correspondingGraphNodes.ElementAt(0);
                            foreach (BasicBlockGraphNodeLink tempLink in currentMetadata.correspondingGraphNodes) {
                                if (tempLink.validPathId == sourceLink.validPathId) {
                                    link = tempLink;
                                }
                            }
                            if (link == null) {
                                throw new ArgumentException("Not able to find link from current basic block to the obfuscation graph.");
                            }

                        }

                        // => just use the link from the source basic block to the obfuscation graph
                        else {
                            link = sourceMetadata.correspondingGraphNodes.ElementAt(0);
                        }

                        // decide randomly to add an opaque predicate
                        switch (this.prng.Next(this.insertOpaquePredicateWeight)) {
                            case 0:
                            case 1:
                            case 2:
                            case 3:
                            case 4:
                            case 5: {

                                    // choose randomly which opaque predicate should be added
                                    int opaquePredicate = this.prng.Next(2);
                                    this.injectOpaquePredicateCode(cfgManipulator, methodCfg, currentBasicBlock, entryBranch, link, opaquePredicate, currentNodeLocal);

                                    break;
                                }

                            default: {
                                    break;
                                }
                        }

                    }

                    // remove currently processed basic block from the list of basic blocks to process
                    basicBlocksToProcess.Remove(currentBasicBlock);

                }

                System.Console.WriteLine("Basic Blocks to process (Opaque Predicates): " + basicBlocksToProcess.Count());

                // check if basic blocks exist to process => if not break loop
                if (basicBlocksToProcess.Count() == 0) {
                    break;
                }

            }

        }


        private void removeReplaceUnconditionalBranchesIter(CfgManipulator cfgManipulator, GraphRandomStateGenerator randomStateGenerator, MethodCfg originalMethodCfg, MethodCfg methodCfg, BasicBlock startBasicBlock, LocalDefinition intStateLocal, LocalDefinition currentNodeLocal) {

            // create a list of basic blocks that still have to be processed by the algorithm
            List<BasicBlock> basicBlocksToProcess = new List<BasicBlock>(methodCfg.basicBlocks);

            System.Console.WriteLine("Basic Blocks to process (Replace Unconditional Branches): " + basicBlocksToProcess.Count());

            while (true) {

                List<BasicBlock> copiedList = new List<BasicBlock>(basicBlocksToProcess);
                foreach (BasicBlock currentBasicBlock in copiedList) {

                    // check if basic block has entry branches that can be optimized
                    bool hasUnconditionalBranchTarget = false;
                    bool hasNoBranchTarget = false;
                    foreach (IBranchTarget entryBranch in currentBasicBlock.entryBranches) {
                        if ((entryBranch as NoBranchTarget) != null) {
                            hasNoBranchTarget = true;
                            continue;
                        }
                        else if ((entryBranch as UnconditionalBranchTarget) != null) {
                            hasUnconditionalBranchTarget = true;
                            continue;
                        }
                    }

                    // skip if basic block already has no branch target
                    if (hasNoBranchTarget) {
                        // remove currently processed basic block from the list of basic blocks to process
                        basicBlocksToProcess.Remove(currentBasicBlock);
                        continue;
                    }

                    // skip if basic block does not have an unconditional branch target
                    if (!hasUnconditionalBranchTarget) {
                        // remove currently processed basic block from the list of basic blocks to process
                        basicBlocksToProcess.Remove(currentBasicBlock);
                        continue;
                    }

                    // replace one unconditional branch by a no branch
                    List<IBranchTarget> copiedBranchList = new List<IBranchTarget>(currentBasicBlock.entryBranches);
                    foreach (IBranchTarget entryBranch in copiedBranchList) {

                        if ((entryBranch as UnconditionalBranchTarget) != null) {
                            BasicBlock sourceBasicBlock = entryBranch.sourceBasicBlock;

                            // create replacement branch
                            NoBranchTarget replacementBranch = new NoBranchTarget();
                            replacementBranch.sourceBasicBlock = sourceBasicBlock;
                            replacementBranch.takenTarget = currentBasicBlock;

                            // replace old branch with new one
                            sourceBasicBlock.exitBranch = replacementBranch;
                            currentBasicBlock.entryBranches.Remove(entryBranch);
                            currentBasicBlock.entryBranches.Add(replacementBranch);

                            // replace unconditional branch instruction with nop
                            IOperation lastOperation = sourceBasicBlock.operations.ElementAt(sourceBasicBlock.operations.Count() - 1);
                            if (!CfgBuilder.isUnconditionalBranchOperation(lastOperation)) {
                                throw new ArgumentException("Last instruction of basic block have to be an unconditional branch.");
                            }
                            IOperation replacementOperation = this.helperClass.createNewOperation(OperationCode.Nop);
                            sourceBasicBlock.operations[sourceBasicBlock.operations.Count() - 1] = replacementOperation;

                            break;
                        }

                    }

                    // remove currently processed basic block from the list of basic blocks to process
                    basicBlocksToProcess.Remove(currentBasicBlock);

                }

                System.Console.WriteLine("Basic Blocks to process (Replace Unconditional Branches): " + basicBlocksToProcess.Count());

                // check if basic blocks exist to process => if not break loop
                if (basicBlocksToProcess.Count() == 0) {
                    break;
                }

            }

        }

    }

}
