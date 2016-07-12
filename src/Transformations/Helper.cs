
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
    public class Helper {
        internal class InternalOperation : IOperation {
            public OperationCode OperationCode { get; set; }
            public uint Offset { get; set; }
            public ILocation Location { get; set; }
            public object Value { get; set; }
        }

        Log.Log logger = null;
        PeReader.DefaultHost host = null;
        IModule module = null;

        public INamedTypeDefinition systemString = null;
        public INamedTypeDefinition systemIOTextWriter = null;
        public INamedTypeDefinition systemIOStreamWriter = null;
        public INamedTypeDefinition systemInt32 = null;
        public INamedTypeDefinition systemObject = null;
        public INamedTypeDefinition systemConsole = null;
        public INamedTypeDefinition systemRandom = null;

        public IMethodDefinition stringConcatThree = null;
        public IMethodDefinition stringConcatTwo = null;
        public IMethodDefinition textWriterWriteLine = null;
        public IMethodDefinition streamWriterCtor = null;
        public IMethodDefinition streamWriterCtorAppend = null;
        public IMethodDefinition textWriterWrite = null;
        public IMethodDefinition int32ToString = null;
        public IMethodDefinition textWriterClose = null;
        public IMethodDefinition objectGetHashCode = null;
        public IMethodDefinition systemConsoleWriteLine = null;
        public IMethodDefinition objectCtor = null;
        public IMethodDefinition systemRandomCtor = null;
        public IMethodDefinition systemRandomNext = null;


        public Helper(IModule module, PeReader.DefaultHost host, Log.Log logger)
        {
            this.host = host;
            this.logger = logger;
            this.module = module;

            // get all needed functions and namespaces
            this.systemString = UnitHelper.FindType(this.host.NameTable, this.host.LoadAssembly(this.host.CoreAssemblySymbolicIdentity), "System.String");
            this.systemIOTextWriter = UnitHelper.FindType(this.host.NameTable, this.host.LoadAssembly(this.host.CoreAssemblySymbolicIdentity), "System.IO.TextWriter");
            this.systemIOStreamWriter = UnitHelper.FindType(this.host.NameTable, this.host.LoadAssembly(this.host.CoreAssemblySymbolicIdentity), "System.IO.StreamWriter");
            this.systemInt32 = UnitHelper.FindType(this.host.NameTable, this.host.LoadAssembly(this.host.CoreAssemblySymbolicIdentity), "System.Int32");
            this.systemObject = UnitHelper.FindType(this.host.NameTable, this.host.LoadAssembly(this.host.CoreAssemblySymbolicIdentity), "System.Object");
            this.systemConsole = UnitHelper.FindType(this.host.NameTable, this.host.LoadAssembly(this.host.CoreAssemblySymbolicIdentity), "System.Console");
            this.systemRandom = UnitHelper.FindType(this.host.NameTable, this.host.LoadAssembly(this.host.CoreAssemblySymbolicIdentity), "System.Random");

            ITypeReference[] concatThreeParameterTypes = { this.host.PlatformType.SystemString, this.host.PlatformType.SystemString, this.host.PlatformType.SystemString };
            ITypeReference[] concatTwoParameterTypes = { this.host.PlatformType.SystemString, this.host.PlatformType.SystemString };
            ITypeReference[] streamWriterAppendTypes = { this.host.PlatformType.SystemString, this.host.PlatformType.SystemBoolean };

            this.stringConcatThree = TypeHelper.GetMethod(systemString, this.host.NameTable.GetNameFor("Concat"), concatThreeParameterTypes);
            this.stringConcatTwo = TypeHelper.GetMethod(systemString, this.host.NameTable.GetNameFor("Concat"), concatTwoParameterTypes);
            this.textWriterWriteLine = TypeHelper.GetMethod(systemIOTextWriter, this.host.NameTable.GetNameFor("WriteLine"), this.host.PlatformType.SystemString);
            this.streamWriterCtor = TypeHelper.GetMethod(systemIOStreamWriter, this.host.NameTable.GetNameFor(".ctor"), this.host.PlatformType.SystemString);
            this.streamWriterCtorAppend = TypeHelper.GetMethod(systemIOStreamWriter, this.host.NameTable.GetNameFor(".ctor"), streamWriterAppendTypes);
            this.textWriterWrite = TypeHelper.GetMethod(systemIOTextWriter, this.host.NameTable.GetNameFor("Write"), this.host.PlatformType.SystemString);
            this.int32ToString = TypeHelper.GetMethod(systemInt32, this.host.NameTable.GetNameFor("ToString"));
            this.textWriterClose = TypeHelper.GetMethod(systemIOTextWriter, this.host.NameTable.GetNameFor("Close"));
            this.objectGetHashCode = TypeHelper.GetMethod(systemObject, this.host.NameTable.GetNameFor("GetHashCode"));
            this.systemConsoleWriteLine = TypeHelper.GetMethod(systemConsole, host.NameTable.GetNameFor("WriteLine"), host.PlatformType.SystemString);
            this.objectCtor = TypeHelper.GetMethod(systemObject, this.host.NameTable.GetNameFor(".ctor"));
            this.systemRandomCtor = TypeHelper.GetMethod(systemRandom, this.host.NameTable.GetNameFor(".ctor"));
            this.systemRandomNext = TypeHelper.GetMethod(systemRandom, this.host.NameTable.GetNameFor("Next"), host.PlatformType.SystemInt32);
        }

        // this method creates a new class for the given namespace
        public NamespaceTypeDefinition createNewClass(String className, IUnitNamespace containingUnitNamespace, bool isPublic,
            bool isStatic)
        {
            // create a new class object
            NamespaceTypeDefinition newClass = new NamespaceTypeDefinition();
            newClass.ContainingUnitNamespace = containingUnitNamespace;
            newClass.InternFactory = this.host.InternFactory;
            newClass.IsClass = true;
            newClass.IsForeignObject = false;
            newClass.IsInterface = false;
            newClass.IsPublic = isPublic;
            newClass.IsStatic = isStatic;
            newClass.Methods = new List<IMethodDefinition>();
            newClass.Name = this.host.NameTable.GetNameFor(className);

            // create list of base classes (only base class is System.Object)
            newClass.BaseClasses = new List<ITypeReference>();
            newClass.BaseClasses.Add(this.host.PlatformType.SystemObject);

            // add new class to module assembly
            Assembly tempAssembly = (Assembly)this.module;
            tempAssembly.AllTypes.Add(newClass);

            return newClass;
        }
        // this method creates an new interface for the given namespace
        public NamespaceTypeDefinition createNewInterface(String className, IUnitNamespace containingUnitNamespace)
        {

            // create a new class object and modify it to be an interface
            NamespaceTypeDefinition newInterface = this.createNewClass(className, containingUnitNamespace, false, false);
            newInterface.IsAbstract = true;
            newInterface.IsInterface = true;
            newInterface.BaseClasses = null;

            return newInterface;
        }


        // this method creates a new method for the given class
        public MethodDefinition createNewMethod(String methodName, NamespaceTypeDefinition methodClass, ITypeReference methodType,
            TypeMemberVisibility methodVisibility, List<IParameterDefinition> methodParameters,
            CallingConvention methodCallingConvention, bool isStatic, bool isAbstract, bool isVirtual)
        {
            // create a new method
            MethodDefinition newMethod = new MethodDefinition();
            newMethod.ContainingTypeDefinition = methodClass;
            newMethod.InternFactory = this.host.InternFactory;
            newMethod.IsCil = true;
            newMethod.IsStatic = isStatic;
            newMethod.Name = this.host.NameTable.GetNameFor(methodName);
            newMethod.Type = methodType;
            newMethod.Visibility = methodVisibility;
            newMethod.IsAbstract = isAbstract;
            newMethod.IsVirtual = isVirtual;
            newMethod.Parameters = methodParameters;
            newMethod.CallingConvention = methodCallingConvention;

            // add method to class
            if (methodClass.Methods == null) {
                methodClass.Methods = new List<IMethodDefinition>();
                methodClass.Methods.Add(newMethod);
            }
            else {
                methodClass.Methods.Add(newMethod);
            }

            return newMethod;
        }

        // makes a copy of the method
        public void copyMethod(MethodDefinition dest, IMethodDefinition source)
        {
            // only copy body if it is not a dummy (= empty)
            if (!(source.Body is Microsoft.Cci.Dummy)) {
                // copy instructions
                ILGenerator ilGenerator = new ILGenerator(this.host, dest);
                foreach (var operation in source.Body.Operations) {
                    ilGenerator.Emit(operation.OperationCode, operation.Value);
                }

                // copy the exception handler
                foreach (IOperationExceptionInformation exceptionToCopy in source.Body.OperationExceptionInformation) {
                    ILGeneratorLabel tryStart = new ILGeneratorLabel();
                    tryStart.Offset = exceptionToCopy.TryStartOffset;
                    ILGeneratorLabel tryEnd = new ILGeneratorLabel();
                    tryEnd.Offset = exceptionToCopy.TryEndOffset;
                    ILGeneratorLabel handlerStart = new ILGeneratorLabel();
                    handlerStart.Offset = exceptionToCopy.HandlerStartOffset;
                    ILGeneratorLabel handlerEnd = new ILGeneratorLabel();
                    handlerEnd.Offset = exceptionToCopy.HandlerEndOffset;
                    ILGeneratorLabel filterStart = new ILGeneratorLabel();
                    filterStart.Offset = exceptionToCopy.FilterDecisionStartOffset;

                    ilGenerator.AddExceptionHandlerInformation(exceptionToCopy.HandlerKind, exceptionToCopy.ExceptionType,
                        tryStart, tryEnd, handlerStart, handlerEnd, filterStart);
                }

                // create the body
                List<ILocalDefinition> variableListCopy = new List<ILocalDefinition>(source.Body.LocalVariables);
                List<ITypeDefinition> privateHelperTypesListCopy = new List<ITypeDefinition>(source.Body.PrivateHelperTypes);
                var newBody = new ILGeneratorMethodBody(ilGenerator, source.Body.LocalsAreZeroed, source.Body.MaxStack, dest,
                    variableListCopy, privateHelperTypesListCopy);
                dest.Body = newBody;
            }

            dest.CallingConvention = source.CallingConvention;
            if (source.IsGeneric)
                dest.GenericParameters = new List<IGenericMethodParameter>(source.GenericParameters);
            else
                dest.GenericParameters = null;
            if (source.ParameterCount > 0)
                dest.Parameters = new List<IParameterDefinition>(source.Parameters);
            else
                dest.Parameters = null;
            if (source.IsPlatformInvoke)
                dest.PlatformInvokeData = source.PlatformInvokeData;
            else
                dest.PlatformInvokeData = Dummy.PlatformInvokeInformation;
            dest.ReturnValueAttributes = new List<ICustomAttribute>(source.ReturnValueAttributes);
            if (source.ReturnValueIsModified)
                dest.ReturnValueCustomModifiers = new List<ICustomModifier>(source.ReturnValueCustomModifiers);
            else
                dest.ReturnValueCustomModifiers = new List<ICustomModifier>(0);
            if (source.ReturnValueIsMarshalledExplicitly)
                dest.ReturnValueMarshallingInformation = source.ReturnValueMarshallingInformation;
            else
                dest.ReturnValueMarshallingInformation = Dummy.MarshallingInformation;
            if (source.HasDeclarativeSecurity && IteratorHelper.EnumerableIsNotEmpty(source.SecurityAttributes))
                dest.SecurityAttributes = new List<ISecurityAttribute>(source.SecurityAttributes);
            else
                dest.SecurityAttributes = null;
            dest.Type = source.Type;
            dest.AcceptsExtraArguments = source.AcceptsExtraArguments;
            dest.HasDeclarativeSecurity = source.HasDeclarativeSecurity;
            dest.IsAbstract = source.IsAbstract;
            dest.IsAccessCheckedOnOverride = source.IsAccessCheckedOnOverride;
            dest.IsCil = source.IsCil;
            dest.IsExternal = source.IsExternal;
            dest.IsForwardReference = source.IsForwardReference;
            dest.IsHiddenBySignature = source.IsHiddenBySignature;
            dest.IsNativeCode = source.IsNativeCode;
            dest.IsNewSlot = source.IsNewSlot;
            dest.IsNeverInlined = source.IsNeverInlined;
            dest.IsAggressivelyInlined = source.IsAggressivelyInlined;
            dest.IsNeverOptimized = source.IsNeverOptimized;
            dest.IsPlatformInvoke = source.IsPlatformInvoke;
            dest.IsRuntimeImplemented = source.IsRuntimeImplemented;
            dest.IsRuntimeInternal = source.IsRuntimeInternal;
            dest.IsRuntimeSpecial = source.IsRuntimeSpecial;
            dest.IsSealed = source.IsSealed;
            dest.IsSpecialName = source.IsSpecialName;
            dest.IsStatic = source.IsStatic;
            dest.IsSynchronized = source.IsSynchronized;
            dest.IsUnmanaged = source.IsUnmanaged;
            if (dest.IsStatic)
                dest.IsVirtual = false;
            else
                dest.IsVirtual = source.IsVirtual;
            dest.PreserveSignature = source.PreserveSignature;
            dest.RequiresSecurityObject = source.RequiresSecurityObject;
            dest.ReturnValueIsByRef = source.ReturnValueIsByRef;
            dest.ReturnValueIsMarshalledExplicitly = source.ReturnValueIsMarshalledExplicitly;
            dest.ReturnValueName = source.ReturnValueName;
            dest.Name = source.Name;
            dest.Visibility = source.Visibility;
        }

        // makes a copy of the field
        public void copyField(FieldDefinition dest, FieldDefinition source)
        {
            if (source.IsBitField)
                dest.BitLength = (uint)source.BitLength;
            else
                dest.BitLength = uint.MaxValue;
            dest.CompileTimeValue = source.CompileTimeValue;
            dest.IsCompileTimeConstant = source.IsCompileTimeConstant;
            if (source.IsModified)
                dest.CustomModifiers = new List<ICustomModifier>(source.CustomModifiers);
            else
                dest.CustomModifiers = null;
            if (source.IsMapped)
                dest.FieldMapping = source.FieldMapping;
            else
                dest.FieldMapping = Dummy.SectionBlock;
            if (source.IsMarshalledExplicitly)
                dest.MarshallingInformation = source.MarshallingInformation;
            else
                dest.MarshallingInformation = Dummy.MarshallingInformation;
            if (source.ContainingTypeDefinition.Layout == LayoutKind.Explicit)
                dest.Offset = source.Offset;
            else
                dest.Offset = 0;
            if (source.ContainingTypeDefinition.Layout == LayoutKind.Sequential)
                dest.SequenceNumber = source.SequenceNumber;
            else
                dest.SequenceNumber = 0;
            dest.Type = source.Type;
            dest.IsNotSerialized = source.IsNotSerialized;
            dest.IsReadOnly = source.IsReadOnly;
            dest.IsSpecialName = source.IsSpecialName;
            if (source.IsRuntimeSpecial) {
                //^ assume dest.IsSpecialName;
                dest.IsRuntimeSpecial = source.IsRuntimeSpecial;
            }
            else {
                dest.IsRuntimeSpecial = false;
            }
            dest.IsStatic = source.IsStatic;
            dest.Name = source.Name;
            dest.Visibility = source.Visibility;
        }

        // creates a new CCI operation
        public IOperation createNewOperation(OperationCode operationCode, object value=null, ILocation location=null,
            uint offset=0)
        {
            InternalOperation newOperation = new InternalOperation();
            newOperation.OperationCode = operationCode;
            newOperation.Value = value;
            newOperation.Location = location;
            newOperation.Offset = offset;

            return newOperation;
        }
    }

    public class CodeMutator {
        // Internal class that represents an operation.
        internal class InternalOperation : IOperation {
            public OperationCode OperationCode { get; set; }
            public uint Offset { get; set; }
            public ILocation Location { get; set; }
            public object Value { get; set; }
        }

        // Creates a new CCI operation.
        private static IOperation createNewOperation(OperationCode operationCode, object value=null,
            ILocation location=null, uint offset=0)
        {
            InternalOperation newOperation = new InternalOperation();
            newOperation.OperationCode = operationCode;
            newOperation.Value = value;
            newOperation.Location = location;
            newOperation.Offset = offset;

            return newOperation;
        }

        Log.Log logger = null;
        PeReader.DefaultHost host = null;
        IModule module = null;
        Random prng = null;

        MethodCfg methodCfg = null;
        CfgManipulator manipulator = null;

        bool debugging = false;
        BasicBlock returnBlock = null;

        public CodeMutator(IModule module, PeReader.DefaultHost host, Log.Log logger, Random prng, MethodCfg methodCfg,
            CfgManipulator manipulator, bool debugging=false)
        {
            this.host = host;
            this.logger = logger;
            this.module = module;
            this.prng = prng;
            this.methodCfg = methodCfg;
            this.debugging = debugging;
            this.manipulator = manipulator;

            returnBlock = new BasicBlock();
            var exitBranch = new ExitBranchTarget();

            returnBlock.exitBranch = exitBranch;
            returnBlock.operations.Add(createNewOperation(OperationCode.Ret));

            methodCfg.basicBlocks.Add(returnBlock);
        }

        private void mutateRet(BasicBlock block, int index)
        {
            if(prng.Next(3) == 0) {
                return;
            }

            var exitBranch = new UnconditionalBranchTarget();
            exitBranch.takenTarget = returnBlock;
            exitBranch.sourceBasicBlock = block;

            block.exitBranch = exitBranch;
            returnBlock.entryBranches.Add(exitBranch);

            var branchToReturn = createNewOperation(OperationCode.Br, 0);
            block.operations[index] = branchToReturn;
            block.semanticId = -1;
        }

        private void splitBlocks(List<BasicBlock> blocks)
        {
            // Determine expected block length.
            int blockLength = 0;
            foreach(var block in methodCfg.basicBlocks) {
                blockLength += block.operations.Count;
            }

            blockLength /= methodCfg.basicBlocks.Count;

            var blocksToSplit = new List<Tuple<BasicBlock, int>>();
            foreach(var block in blocks) {
                if(block.operations.Count > blockLength) {
                    var variance = block.operations.Count - blockLength;
                    if(variance > blockLength / 2) {
                        variance = blockLength / 2;
                    }

                    variance = prng.Next(-variance, variance);
                    blocksToSplit.Add(new Tuple<BasicBlock, int>(block, blockLength + variance));
                }
            }

            // Split blocks exceeding the length (+ vary length within a certain range).
            foreach(var info in blocksToSplit) {
                manipulator.splitBasicBlock(info.Item1, info.Item2);
            }
        }

        public void mutateSemanticallyEquivalentBlocks(List<BasicBlock> blocks)
        {
            if(this.debugging || blocks.Count() == 0) {
                return;
            }

            mutateBlocks(blocks);
        }

        public void mutateBlocks(List<BasicBlock> blocks)
        {
            if(this.debugging || blocks.Count() == 0) {
                return;
            }

            // Pattern-based replacement.
            foreach(var block in blocks) {
                for(int i = 0; i < block.operations.Count; ++i) {

                    switch(block.operations[i].OperationCode) {
                    case OperationCode.Ret:
                        mutateRet(block, i);
                        break;
                    }
                }
            }

            // Split blocks last to ensure proper/efficient mutations.
            splitBlocks(blocks);

            // First pass: Simply pattern-based replacement of instructions - independent from other blocks.
            // Second pass: Try to re-order instructions?
            // Third pass: Even add more difficult control flow (not only focused on one single basic block)?
            // Other: Add temporary locals? [Maybe re-use locals used for dead code? Would require metadata for locals.]
            // [x] Add new ret blocks?
            // Handle {cgt, clt, ceq}.un explicitly using new blocks.
            // Call: Change to calli?
            // Generate immediates using simple arithmetic.
            // Maybe "cache" the string variables in new locals initialized in the very first block.
            // Split basic block.
        } 
    }

    public class CodeGenerator {
        // an internal class that represents an operation
        // (the attributes of the CCI operations are not writeable)
        internal class InternalOperation : IOperation {
            public OperationCode OperationCode { get; set; }
            public uint Offset { get; set; }
            public ILocation Location { get; set; }
            public object Value { get; set; }
        }

        // creates a new CCI operation
        private static IOperation createNewOperation(OperationCode operationCode, object value=null,
            ILocation location=null, uint offset=0)
        {
            InternalOperation newOperation = new InternalOperation();
            newOperation.OperationCode = operationCode;
            newOperation.Value = value;
            newOperation.Location = location;
            newOperation.Offset = offset;

            return newOperation;
        }

        Log.Log logger = null;
        PeReader.DefaultHost host = null;
        IModule module = null;
        Random prng = null;

        MethodCfg methodCfg = null;
        CfgManipulator manipulator = null;

        List<IMethodDefinition> callableMethods = new List<IMethodDefinition>();
        List<LocalDefinition> deadLocals = new List<LocalDefinition>();

        // TODO: Expand this list. Also: Keep in sync with ILocalDefinition addDeadLocal(string targetType).
        List<string> supportedVariableTypes = new List<string> { "System.Void", "System.Int32", "System.String", "System.Boolean" };

        bool debugging = false;
        int debuggingValue = 0;

        List<IMethodDefinition> retrieveCallableMethods()
        {
            var results = new List<IMethodDefinition>();
            foreach(var method in methodCfg.method.ContainingTypeDefinition.Methods) {
                // Check method type.
                bool isSpecial = method.IsConstructor || method.IsAbstract || method.IsExternal || method.IsStatic;
                if(isSpecial) {
                    continue;
                }

                // Check parameters.
                bool supported = true;
                foreach(var parameter in method.Parameters) {
                    string type = parameter.Type.ToString();
                    if(!supportedVariableTypes.Contains(type)) {
                        supported = false;
                        break;
                    }
                }
                
                if(!supported) {
                    continue;
                }

                // Check local variables. (TODO: Add one if missing).
                if(method.Type.ToString() != "System.Void") {
                    bool gotLocal = false;
                    foreach(var local in methodCfg.method.Body.LocalVariables) {
                        if(local.Type.ToString() == method.Type.ToString()) {
                            gotLocal = true;
                            break;
                        }
                    }

                    if(!gotLocal) {
                        continue;
                    }
                }

                results.Add(method);
            }

            return results;
        }

        public CodeGenerator(IModule module, PeReader.DefaultHost host, Log.Log logger, Random prng, MethodCfg methodCfg,
            bool debugging=false, CfgManipulator manipulator=null)
        {
            this.host = host;
            this.logger = logger;
            this.module = module;
            this.prng = prng;
            this.methodCfg = methodCfg;
            this.debugging = debugging;
            this.manipulator = manipulator;

            callableMethods = retrieveCallableMethods();
        }

        public static List<IOperation> generateDeadCode(bool lastOperationRet=false)
        {
            var result = new List<IOperation>();
            /*result.Add(createNewOperation(OperationCode.Nop));
            result.Add(createNewOperation(OperationCode.Nop));
            result.Add(createNewOperation(OperationCode.Nop));
            result.Add(createNewOperation(OperationCode.Nop));*/

            if(lastOperationRet) {
                result.Add(createNewOperation(OperationCode.Ret));
            }

            return result;
        }

        BasicBlock randomTargetBlock()
        {
            // TODO: Fix for try/catch.

            int index = prng.Next(methodCfg.basicBlocks.Count());
            BasicBlock candidate = methodCfg.basicBlocks.ElementAt(index);

            if(candidate.tryBlocks.Count() != 0 || candidate.handlerBlocks.Count() != 0) {
                throw new ArgumentException("Cannot choose dead code target block within try/handler block.");
            }

            return candidate;
        }

        ILocalDefinition addDeadLocal(string targetType)
        {
            LocalDefinition result = new LocalDefinition();

            result.IsReference = result.IsPinned = result.IsModified = false;
            result.MethodDefinition = methodCfg.method;

            // Switch for local type (TODO: Support more).
            // Maybe create multiple variables of the same type to add diffusion (but add upper bound).
            INamespaceTypeReference type = null;

            switch(targetType) {
            case "System.Int32":   type = host.PlatformType.SystemInt32; break;
            case "System.String":  type = host.PlatformType.SystemString; break;
            case "System.Boolean": type = host.PlatformType.SystemBoolean; break;
            default:
                throw new ArgumentException("Cannot add dead local of unsupported type.");
            }

            // Add the new local variable to our method.
            result.Type = type;
            manipulator.addLocalVariable(result);

            // Initialize dead local variable in the method's first basic block according to its type.
            // TODO: This might be used to easily detect dead locals. Improve on this.
            var loadSequence = loadBogusParameter(type);
            loadSequence.Add(createNewOperation(OperationCode.Stloc, result));

            methodCfg.startBasicBlock.operations.InsertRange(0, loadSequence);
            return result;
        }

        ILocalDefinition getSuitableDeadLocal(string targetType)
        {
            var matchingLocals = new List<ILocalDefinition>();
            ILocalDefinition result = null;

            foreach(var local in deadLocals) {
                if(local.Type.ToString() == targetType) {
                    matchingLocals.Add(local);
                }
            }

            if(matchingLocals.Count() > 0) {
                int index = prng.Next(matchingLocals.Count());
                result = matchingLocals.ElementAt(index);
            }

            return result;
        }

        List<ILocalDefinition> getSuitableLocals(string targetType)
        {
            var results = new List<ILocalDefinition>();
            foreach(var local in methodCfg.method.Body.LocalVariables) {
                if(local.Type.ToString() == targetType) {
                    results.Add(local);
                }
            }

            return results;
        }

        void createUnconditionalBranch(BasicBlock deadCodeBasicBlock, ITransformationMetadata basicBlockMetadata)
        {
            // Branch to some other basic block in our method (or to a newly created dead code block).
            var exitBranch = new UnconditionalBranchTarget();
            exitBranch.sourceBasicBlock = deadCodeBasicBlock;
            deadCodeBasicBlock.exitBranch = exitBranch;

            deadCodeBasicBlock.operations.Add(createNewOperation(OperationCode.Br, 0));

            // TODO: Decrease possibility for dead code to be added?
            if(prng.Next(2) == 0) { 
                // Random BB as dead code block target.
                var targetBlock = randomTargetBlock();

                targetBlock.entryBranches.Add(exitBranch);
                exitBranch.takenTarget = targetBlock;
            }
            else {
                // New dead code BB as dead code block target.
                var targetBlock = new BasicBlock();
                targetBlock.startIdx = targetBlock.endIdx = 0;

                targetBlock.entryBranches.Add(exitBranch);
                exitBranch.takenTarget = targetBlock;

                generateDeadCode(targetBlock, basicBlockMetadata);
            }
        }

        void createExitBranch(BasicBlock deadCodeBasicBlock)
        {
            // Exit the function at this dead code block while also returning a sane local as return value.
            var exitBranch = new ExitBranchTarget();
            exitBranch.sourceBasicBlock = deadCodeBasicBlock;
            deadCodeBasicBlock.exitBranch = exitBranch;

            if(methodCfg.method.Type.ToString() != "System.Void") {
                var locals = getSuitableLocals(methodCfg.method.Type.ToString());
                if(locals.Count() == 0) {
                    // TODO: Actually support the return here (e.g., by adding proper locals).
                    throw new ArgumentException("Cannot yet handle creation of exit branches for non-void methods without" +
                        " suitable locals.");
                }

                int index = prng.Next(locals.Count());
                var local = methodCfg.method.Body.LocalVariables.ElementAt(index);

                deadCodeBasicBlock.operations.Add(createNewOperation(OperationCode.Ldloc, local));
            }

            deadCodeBasicBlock.operations.Add(createNewOperation(OperationCode.Ret));
        }

        void createNoBranch(BasicBlock deadCodeBasicBlock)
        {
            // Set the dead code block above some other block.
            var exitBranch = new NoBranchTarget();
            exitBranch.sourceBasicBlock = deadCodeBasicBlock;
            deadCodeBasicBlock.exitBranch = exitBranch;

            BasicBlock targetBlock = null;
            do {
                targetBlock = randomTargetBlock();

                // Skip blocks that already have a "no branch" as entry.
                foreach(var entryBranch in targetBlock.entryBranches) {
                    if(entryBranch is NoBranchTarget) {
                        targetBlock = null;
                        break;
                    }
                }
            } while(targetBlock == null);

            targetBlock.entryBranches.Add(exitBranch);
            exitBranch.takenTarget = targetBlock;
        }

        public void generateDeadCode(BasicBlock deadCodeBasicBlock, ITransformationMetadata basicBlockMetadata)
        {
            // Add the dead code to the basic block.
            var result = generateDeadCode();
            foreach(var operation in result) {
                deadCodeBasicBlock.operations.Add(operation);
            }

            // Add metadata, if necessary (TODO: Use a set for metadata handling?)
            if(basicBlockMetadata != null && !deadCodeBasicBlock.transformationMetadata.Contains(basicBlockMetadata)) {
                deadCodeBasicBlock.transformationMetadata.Add(basicBlockMetadata);
            }

            // Choose epilogue of dead code block.
            int target = 1;
            if(!debugging) {
                target = prng.Next(3);
                if(prng.Next(1) == 0) {
                    target = 0;
                }
            }

            switch(target) {
                case 0: createUnconditionalBranch(deadCodeBasicBlock, basicBlockMetadata); break;
                case 1: createExitBranch(deadCodeBasicBlock); break;
                case 2: createNoBranch(deadCodeBasicBlock); break;
            }
        }

        public List<IOperation> generateDeadCode()
        {
            var result = new List<IOperation>();
            if(debugging) {
                for(int i = 0; i < 5; ++i) {
                    result.Add(createNewOperation(OperationCode.Nop));
                }

                // Write dead code position to ouput. -- IGNORED FOR NOW.
                var coreAssembly = host.LoadAssembly(host.CoreAssemblySymbolicIdentity); // NEED IT FOR DEBUGGING
                var systemConsole = UnitHelper.FindType(host.NameTable, coreAssembly, "System.Console");
                var writeLine = TypeHelper.GetMethod(systemConsole, host.NameTable.GetNameFor("WriteLine"),
                    host.PlatformType.SystemString);

                result.Add(createNewOperation(OperationCode.Ldstr, "Dead code " + debuggingValue.ToString()));
                result.Add(createNewOperation(OperationCode.Call, writeLine));

                debuggingValue++;
                //return result; -- TESTING BOGUS CALLS.
            }

            result.AddRange(insertBogusCall());

            // Add a pass which operates on locals depending on the "method trait" (e.g., add string operations, if the method is
            // marked as string-, arithmetic- or OOP-centric).
            // For methods with "recursion-trait": Call itself with a hardcoded random path.
            // Sidenote: Recursion-based obfuscation? SOmething like recursive flattening?

            // Copy existing basic blocks (non-obfu ones, see semantic ID) and modify them slightly (cf. dead code, but no restriction
            // regarding semantics). Increment/decrement integer parameters, backtrack enums and use another entry (too much?),
            // modify strings, replace references to references to locals of the same type, ...
            // How are the locals used within the method?
            return result;
        }

        private List<IOperation> loadBogusParameter(ITypeReference type)
        {
            return loadBogusParameter(type.ToString());
        }

        private List<IOperation> loadBogusParameter(string typeString)
        {
            // TODO: Either load bogus values (hardcoded), locals or even arguments.
            var result = new List<IOperation>();

            // In two of three cases, use a local to pass as parameter (if found at all).
            var locals = getSuitableLocals(typeString);
            if(locals.Count() != 0 && prng.Next(3) != 1) {
                var index = prng.Next(locals.Count());
                var local = locals.ElementAt(index);

                result.Add(createNewOperation(OperationCode.Ldloc, local));
                return result;
            }

            switch(typeString) {
            case "System.Int32":
                // TODO: ldc.{number} for lower numbers? Sane range?
                result.Add(createNewOperation(OperationCode.Ldc_I4, prng.Next(10)));
                break;
            
            case "System.String":
                // TODO: Search other strings used in method, truncate/substring/...
                result.Add(createNewOperation(OperationCode.Ldstr, prng.Next(10).ToString()));
                break;
            
            case "System.Boolean":
                result.Add(createNewOperation(OperationCode.Ldc_I4, prng.Next(2)));
                break;

            default:
                throw new ArgumentException("Unsupported bogus parameter, NO GOOD.");
            }

            return result;
        }

        private List<IOperation> insertBogusCall()
        {
            // TODO: In general: Are we necessarily restricted to "this"-calls?
            // May standard runtime calls make sense on existing locals?
            var result = new List<IOperation>();

            var index = prng.Next(callableMethods.Count());
            var targetMethod = callableMethods.ElementAt(index);

            // Handle parameters.
            foreach(var parameter in targetMethod.Parameters) {
                //throw new ArgumentException("TODO.");
                result.AddRange(loadBogusParameter(parameter.Type));

                // TODO: Inspect existing calls to method, try to backtrace parameters.
                // Maybe classify those that are this-calls and those to the runtime/other objects (?).
            }

            // Load "this" pointer and call the method.
            result.Add(createNewOperation(OperationCode.Ldarg_0));
            result.Add(createNewOperation(OperationCode.Callvirt, targetMethod));

            // Handle return value.
            var returnType = targetMethod.Type.ToString();
            if(returnType != "System.Void") {

                ILocalDefinition local = null;
                var locals = getSuitableLocals(returnType);

                if(locals.Count() == 0) {
                    // Try to add a new local if none is available.
                    local = addDeadLocal(returnType);

                    if(local == null) {
                        throw new ArgumentException("Cannot add dead local of expected type (to be used for bogus call return value).");
                        // TODO: Add new (fake) variables, maybe also update them in legit code? Increases inter-dependencies.
                    }
                }
                else {
                    var localIndex = prng.Next(locals.Count());
                    local = locals.ElementAt(localIndex);
                }

                // TODO: Maybe even use stloc.{number} for low-indexed locals, optimization.
                result.Add(createNewOperation(OperationCode.Stloc, local));
            }

            return result;
        }
    }
}