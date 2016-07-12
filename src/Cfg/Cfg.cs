using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Cci;
using Microsoft.Cci.MutableCodeModel;
using Log;
using CfgElements;


namespace Cfg {

    // this class is able to: (cci) class => CFG and CFG => (cci) class
    public class CfgBuilder {

        // check if the given operation is a branch operation
        public static bool isBranchOperation(IOperation operation) {

            switch (operation.OperationCode) {

                case OperationCode.Beq:
                case OperationCode.Bge:
                case OperationCode.Bge_Un:
                case OperationCode.Bgt:
                case OperationCode.Bgt_Un:
                case OperationCode.Ble:
                case OperationCode.Ble_Un:
                case OperationCode.Blt:
                case OperationCode.Blt_Un:
                case OperationCode.Bne_Un:
                case OperationCode.Brfalse:
                case OperationCode.Brtrue:
                case OperationCode.Beq_S:
                case OperationCode.Bge_S:
                case OperationCode.Bge_Un_S:
                case OperationCode.Bgt_S:
                case OperationCode.Bgt_Un_S:
                case OperationCode.Ble_S:
                case OperationCode.Ble_Un_S:
                case OperationCode.Blt_S:
                case OperationCode.Blt_Un_S:
                case OperationCode.Bne_Un_S:
                case OperationCode.Br:
                case OperationCode.Br_S:
                case OperationCode.Brfalse_S:
                case OperationCode.Brtrue_S:
                case OperationCode.Leave:
                case OperationCode.Leave_S:
                case OperationCode.Switch:
                    return true;

                default:
                    return false;
            }
        }


        // check if the given operation is an unconditional branch operation
        public static bool isUnconditionalBranchOperation(IOperation operation) {

            switch (operation.OperationCode) {
                case OperationCode.Br:
                case OperationCode.Br_S:
                    return true;

                default:
                    return false;
            }
        }


        // gets the size of the argument of the instruction
        public static uint getSizeOfOpcodeArgument(IOperation operation) {
            switch (operation.OperationCode) {

                // instructions with no argument
                case OperationCode.Add:
                case OperationCode.Add_Ovf:
                case OperationCode.Add_Ovf_Un:
                case OperationCode.And:
                case OperationCode.Arglist:
                case OperationCode.Ceq:
                case OperationCode.Cgt:
                case OperationCode.Cgt_Un:
                case OperationCode.Ckfinite:
                case OperationCode.Clt:
                case OperationCode.Clt_Un:
                case OperationCode.Conv_I:
                case OperationCode.Conv_I1:
                case OperationCode.Conv_I2:
                case OperationCode.Conv_I4:
                case OperationCode.Conv_I8:
                case OperationCode.Conv_Ovf_I:
                case OperationCode.Conv_Ovf_I_Un:
                case OperationCode.Conv_Ovf_I1:
                case OperationCode.Conv_Ovf_I1_Un:
                case OperationCode.Conv_Ovf_I2:
                case OperationCode.Conv_Ovf_I2_Un:
                case OperationCode.Conv_Ovf_I4:
                case OperationCode.Conv_Ovf_I4_Un:
                case OperationCode.Conv_Ovf_I8:
                case OperationCode.Conv_Ovf_I8_Un:
                case OperationCode.Conv_Ovf_U:
                case OperationCode.Conv_Ovf_U_Un:
                case OperationCode.Conv_Ovf_U1:
                case OperationCode.Conv_Ovf_U1_Un:
                case OperationCode.Conv_Ovf_U2:
                case OperationCode.Conv_Ovf_U2_Un:
                case OperationCode.Conv_Ovf_U4:
                case OperationCode.Conv_Ovf_U4_Un:
                case OperationCode.Conv_Ovf_U8:
                case OperationCode.Conv_Ovf_U8_Un:
                case OperationCode.Conv_R_Un:
                case OperationCode.Conv_R4:
                case OperationCode.Conv_R8:
                case OperationCode.Conv_U:
                case OperationCode.Conv_U1:
                case OperationCode.Conv_U2:
                case OperationCode.Conv_U4:
                case OperationCode.Conv_U8:
                case OperationCode.Cpblk:
                case OperationCode.Div:
                case OperationCode.Div_Un:
                case OperationCode.Dup:
                //case OperationCode.Endfault:
                case OperationCode.Endfilter:
                case OperationCode.Endfinally:
                case OperationCode.Initblk:
                case OperationCode.Ldarg_0:
                case OperationCode.Ldarg_1:
                case OperationCode.Ldarg_2:
                case OperationCode.Ldarg_3:
                case OperationCode.Ldc_I4_0:
                case OperationCode.Ldc_I4_1:
                case OperationCode.Ldc_I4_2:
                case OperationCode.Ldc_I4_3:
                case OperationCode.Ldc_I4_4:
                case OperationCode.Ldc_I4_5:
                case OperationCode.Ldc_I4_6:
                case OperationCode.Ldc_I4_7:
                case OperationCode.Ldc_I4_8:
                case OperationCode.Ldc_I4_M1:
                case OperationCode.Ldelem_I:
                case OperationCode.Ldelem_I1:
                case OperationCode.Ldelem_I2:
                case OperationCode.Ldelem_I4:
                case OperationCode.Ldelem_I8:
                case OperationCode.Ldelem_R4:
                case OperationCode.Ldelem_R8:
                case OperationCode.Ldelem_Ref:
                case OperationCode.Ldelem_U1:
                case OperationCode.Ldelem_U2:
                case OperationCode.Ldelem_U4:
                //case OperationCode.Ldelem_U8:
                case OperationCode.Ldind_I:
                case OperationCode.Ldind_I1:
                case OperationCode.Ldind_I2:
                case OperationCode.Ldind_I4:
                case OperationCode.Ldind_I8:
                case OperationCode.Ldind_R4:
                case OperationCode.Ldind_R8:
                case OperationCode.Ldind_Ref:
                case OperationCode.Ldind_U1:
                case OperationCode.Ldind_U2:
                case OperationCode.Ldind_U4:
                //case OperationCode.Ldind_U8:
                case OperationCode.Ldlen:
                case OperationCode.Ldloc_0:
                case OperationCode.Ldloc_1:
                case OperationCode.Ldloc_2:
                case OperationCode.Ldloc_3:
                case OperationCode.Ldnull:
                case OperationCode.Localloc:
                case OperationCode.Mul:
                case OperationCode.Mul_Ovf:
                case OperationCode.Mul_Ovf_Un:
                case OperationCode.Neg:
                case OperationCode.Nop:
                case OperationCode.Not:
                case OperationCode.Or:
                case OperationCode.Pop:
                case OperationCode.Refanytype:
                case OperationCode.Rem:
                case OperationCode.Rem_Un:
                case OperationCode.Ret:
                case OperationCode.Rethrow:
                case OperationCode.Shl:
                case OperationCode.Shr:
                case OperationCode.Shr_Un:
                case OperationCode.Stelem_I:
                case OperationCode.Stelem_I1:
                case OperationCode.Stelem_I2:
                case OperationCode.Stelem_I4:
                case OperationCode.Stelem_I8:
                case OperationCode.Stelem_R4:
                case OperationCode.Stelem_R8:
                case OperationCode.Stelem_Ref:
                case OperationCode.Stind_I:
                case OperationCode.Stind_I1:
                case OperationCode.Stind_I2:
                case OperationCode.Stind_I4:
                case OperationCode.Stind_I8:
                case OperationCode.Stind_R4:
                case OperationCode.Stind_R8:
                case OperationCode.Stind_Ref:
                case OperationCode.Stloc_0:
                case OperationCode.Stloc_1:
                case OperationCode.Stloc_2:
                case OperationCode.Stloc_3:
                case OperationCode.Sub:
                case OperationCode.Sub_Ovf:
                case OperationCode.Sub_Ovf_Un:
                case OperationCode.Throw:
                case OperationCode.Volatile_:
                case OperationCode.Xor:
                    return 0;

                // instructions with 1 byte argument
                case OperationCode.Beq_S:
                case OperationCode.Bge_S:
                case OperationCode.Bge_Un_S:
                case OperationCode.Bgt_S:
                case OperationCode.Bgt_Un_S:
                case OperationCode.Ble_S:
                case OperationCode.Ble_Un_S:
                case OperationCode.Blt_S:
                case OperationCode.Blt_Un_S:
                case OperationCode.Bne_Un_S:
                case OperationCode.Br_S:
                case OperationCode.Brfalse_S:
                case OperationCode.Brtrue_S:
                case OperationCode.Ldarg_S:
                case OperationCode.Ldarga_S:
                case OperationCode.Ldc_I4_S:
                case OperationCode.Ldloc_S:
                case OperationCode.Ldloca_S:
                case OperationCode.Leave_S:
                case OperationCode.Starg_S:
                case OperationCode.Stloc_S:
                    return 1;

                // instructions with 2 byte argument
                case OperationCode.Ldarg:
                case OperationCode.Ldarga:
                case OperationCode.Ldloc:
                case OperationCode.Ldloca:
                case OperationCode.Starg:
                case OperationCode.Stloc:
                    return 2;

                // instructions with 4 byte argument
                case OperationCode.Beq:
                case OperationCode.Bge:
                case OperationCode.Bge_Un:
                case OperationCode.Bgt:
                case OperationCode.Bgt_Un:
                case OperationCode.Ble:
                case OperationCode.Ble_Un:
                case OperationCode.Blt:
                case OperationCode.Blt_Un:
                case OperationCode.Bne_Un:
                case OperationCode.Box:
                case OperationCode.Br:
                case OperationCode.Brfalse:
                case OperationCode.Brtrue:
                case OperationCode.Call:
                case OperationCode.Callvirt:
                case OperationCode.Castclass:
                case OperationCode.Constrained_:
                case OperationCode.Cpobj:
                case OperationCode.Initobj:
                case OperationCode.Isinst:
                case OperationCode.Jmp:
                case OperationCode.Ldc_I4:
                case OperationCode.Ldc_R4:
                case OperationCode.Ldelem:
                case OperationCode.Ldelema:
                case OperationCode.Ldfld:
                case OperationCode.Ldflda:
                case OperationCode.Ldftn:
                case OperationCode.Ldobj:
                case OperationCode.Ldsfld:
                case OperationCode.Ldsflda:
                case OperationCode.Ldstr:
                case OperationCode.Ldtoken:
                case OperationCode.Ldvirtftn:
                case OperationCode.Leave:
                case OperationCode.Mkrefany:
                case OperationCode.Newarr:
                case OperationCode.Newobj:
                case OperationCode.Sizeof:
                case OperationCode.Stelem:
                case OperationCode.Stobj:
                case OperationCode.Stfld:
                case OperationCode.Stsfld:
                case OperationCode.Unbox_Any:
                    return 4;

                // instructions with 8 byte argument
                case OperationCode.Ldc_I8:
                case OperationCode.Ldc_R8:
                    return 8;

                // switch is a special case with variable byte length
                case OperationCode.Switch:

                    // switch has one argument with 4 bytes which gives the count
                    // of the jump targets
                    uint tempSize = 4;

                    // each jump target offset uses a 4 byte value
                    tempSize += (uint)((uint[])operation.Value).Count() * 4;

                    return tempSize;

                // argument size unknown
                default:
                    throw new ArgumentException("Do not know how to handle size of " + operation.ToString() + " instruction.");
            }

            /* TODO
             * cases not implemented yet:
                   calli                                      
                   ldfsflda                                                                                              
                   refanyval
                   unbox
             */
        }

        // gets the size of the opcode
        // copied from CCI ILGenerator.cs
        public static uint getSizeOfOpcode(OperationCode opcode) {
            if (((int)opcode) > 0xff && (opcode < OperationCode.Array_Create)) return 2;
            return 1;
        }

        // gets the size of the operation
        public static uint getSizeOfOperation(IOperation operation) {
            return getSizeOfOpcode(operation.OperationCode) + getSizeOfOpcodeArgument(operation);
        }


        // an internal class that is used to get the basic blocks into an order when creating
        // a method from the CFG
        internal class BasicBlockUnion {

            // NOTE: the order of the basic blocks is important! (control flow will be basicBlocks[0] -> basicBlocks[1] -> basicBlocks[2] ...)
            public List<BasicBlock> basicBlocks = new List<BasicBlock>();
        }


        // an internal class that is used to merge the basic block unions together with
        // respect to the exception handler
        internal class BasicBlockUnionExceptionHandlerOrder {

            public IOperationExceptionInformation exceptionHandler;

            public BasicBlockUnion tryStart;
            public IList<BasicBlockUnion> tryBody = new List<BasicBlockUnion>();
            public BasicBlockUnion tryEnd;

            public BasicBlockUnion handlerStart;
            public IList<BasicBlockUnion> handlerBody = new List<BasicBlockUnion>();
            public BasicBlockUnion handlerEnd;
        }


        // an internal class that is used to map the values of the new created exception handler
        // to the object of the old exception handler (is needed to exchange the pointer to the
        // exception handler in the basic blocks with the new exception handler because
        // cci does not give an interface to get the new exception handler objects)
        internal class OldExceptionHandlerMapping {
            public uint tryStartOffset;
            public uint tryEndOffset;
            public uint handlerStartOffset;
            public uint handlerEndOffset;
            public uint filterStartOffset;
            public HandlerKind handlerKind;
            public ITypeReference exceptionType;
            public IOperationExceptionInformation oldExceptionHandler;
            public IOperationExceptionInformation newExceptionHandler;
        }


        // an internal class that represents an operation
        // (the attributes of the CCI operations are not writeable)
        internal class InternalOperation : IOperation {
            public OperationCode OperationCode { get; set; }
            public uint Offset { get; set; }
            public ILocation Location { get; set; }
            public object/*?*/ Value { get; set; }
        }


        Log.Log logger = null;
        PeReader.DefaultHost host = null;
        IModule module = null;

        int semanticId = 0;


        public CfgBuilder(IModule module, PeReader.DefaultHost host, Log.Log logger) {
            this.host = host;
            this.logger = logger;
            this.module = module;
        }


        // build recursively the cfg of the given operations
        private void buildRecursivelyCfg(MethodCfg methodCfg, IEnumerable<IOperation> operations, int currentIdx, IBranchTarget entryBranch, ref BasicBlock currentBasicBlock) {

            // check if a new basic block has to be created
            for (int bbIdx = 0; bbIdx < methodCfg.basicBlocks.Count(); bbIdx++) {

                // check if the current index points into an existing basic block
                // => split basic block
                BasicBlock tempBB = methodCfg.basicBlocks.ElementAt(bbIdx);
                if (tempBB.startIdx < currentIdx && tempBB.endIdx >= currentIdx) {

                    // create new basic block which will be the second half of the found one
                    BasicBlock newBasicBlock = new BasicBlock(this.semanticId);
                    this.semanticId++;
                    newBasicBlock.startIdx = currentIdx;
                    newBasicBlock.endIdx = tempBB.endIdx;
                    tempBB.endIdx = currentIdx - 1;

                    // move the exit basic blocks to the new created basic block
                    // and make the new created basic block the exit of the splitted one                    
                    newBasicBlock.exitBranch = tempBB.exitBranch;
                    newBasicBlock.exitBranch.sourceBasicBlock = newBasicBlock;
                    NoBranchTarget tempExitBranch = new NoBranchTarget();
                    tempExitBranch.takenTarget = newBasicBlock;
                    tempExitBranch.sourceBasicBlock = tempBB;
                    tempBB.exitBranch = tempExitBranch;

                    // set the current basic block to the new created basic block
                    currentBasicBlock = newBasicBlock;

                    // add splitted basic block branch and basic block branch that leads to this split
                    // to the entries of the new one
                    newBasicBlock.entryBranches.Add(entryBranch);
                    newBasicBlock.entryBranches.Add(tempExitBranch);

                    // distribute the instructions to the basic blocks 
                    List<IOperation> previousOperations = new List<IOperation>();
                    List<IOperation> nextOperations = new List<IOperation>();
                    for (int operationIdx = 0; (operationIdx + tempBB.startIdx) <= newBasicBlock.endIdx; operationIdx++) {

                        if ((operationIdx + tempBB.startIdx) < currentIdx) {
                            previousOperations.Add(tempBB.operations.ElementAt(operationIdx));
                        }
                        else {
                            nextOperations.Add(tempBB.operations.ElementAt(operationIdx));
                        }
                    }
                    tempBB.operations = previousOperations;
                    newBasicBlock.operations = nextOperations;

                    // add new basic block to the cfg
                    methodCfg.basicBlocks.Add(newBasicBlock);

                    return;
                }

                // if the current index of the operation points to the start index of an existing basic block
                // => update entry branches
                else if (tempBB.startIdx == currentIdx && entryBranch != null && currentBasicBlock != null) {
                    tempBB.entryBranches.Add(entryBranch);

                    // set the found basic block as the current basic block
                    currentBasicBlock = tempBB;
                    return;
                }

                // if the current index of the operation points to the start index of an existing basic block, has no entry branch
                // and is not the first instruction
                // set the current basic block to the found one
                else if (currentIdx != 0 && tempBB.startIdx == currentIdx && currentBasicBlock != null) {

                    // set the found basic block as the current basic block
                    currentBasicBlock = tempBB;
                    return;

                }

            }

            // set index of current basic block and add it to the cfg
            currentBasicBlock.startIdx = currentIdx;
            methodCfg.basicBlocks.Add(currentBasicBlock);

            // check if the basic block was jumped to from another basic block
            // => update entry branches
            if (entryBranch != null) {
                currentBasicBlock.entryBranches.Add(entryBranch);
            }

            // parse every instruction to find branches etc
            for (int idx = currentIdx; idx < operations.Count(); idx++) {

                // check if the current instruction is the start instruction of an already existing basic block (except the current basic block)
                // => add the current basic block to the list of entry basic blocks, the found basic block to the list of exit basic blocks and set the index
                for (int bbIdx = 0; bbIdx < methodCfg.basicBlocks.Count(); bbIdx++) {
                    BasicBlock tempBB = methodCfg.basicBlocks.ElementAt(bbIdx);
                    if (tempBB.startIdx == idx && tempBB != currentBasicBlock) {
                        currentBasicBlock.endIdx = idx - 1;

                        // create new exit branch and add it
                        NoBranchTarget currentExitBranch = new NoBranchTarget();
                        currentExitBranch.sourceBasicBlock = currentBasicBlock;
                        currentExitBranch.takenTarget = tempBB;
                        currentBasicBlock.exitBranch = currentExitBranch;

                        // add current exit branch as entry for the found one
                        tempBB.entryBranches.Add(currentExitBranch);
                        return;
                    }
                }

                // add current instruction to the basic block
                var operation = operations.ElementAt(idx);
                currentBasicBlock.operations.Add(operation);

                // check for special instructions like branches
                switch (operation.OperationCode) {

                    // conditional branch instructions
                    case OperationCode.Beq:
                    case OperationCode.Bge:
                    case OperationCode.Bge_Un:
                    case OperationCode.Bgt:
                    case OperationCode.Bgt_Un:
                    case OperationCode.Ble:
                    case OperationCode.Ble_Un:
                    case OperationCode.Blt:
                    case OperationCode.Blt_Un:
                    case OperationCode.Bne_Un:
                    case OperationCode.Brfalse:
                    case OperationCode.Brtrue:
                    case OperationCode.Beq_S:
                    case OperationCode.Bge_S:
                    case OperationCode.Bge_Un_S:
                    case OperationCode.Bgt_S:
                    case OperationCode.Bgt_Un_S:
                    case OperationCode.Ble_S:
                    case OperationCode.Ble_Un_S:
                    case OperationCode.Blt_S:
                    case OperationCode.Blt_Un_S:
                    case OperationCode.Bne_Un_S:
                    case OperationCode.Brfalse_S:
                    case OperationCode.Brtrue_S: {

                            // the current basic block ends here
                            currentBasicBlock.endIdx = idx;

                            // calculate the target index of the branch
                            int branchTargetIdx = 0;
                            uint branchTargetOffset;

                            // do operation value can be of type long which can not be casted in this way
                            if (operation.Value is long) {
                                branchTargetOffset = Convert.ToUInt32(operation.Value);
                            }
                            else {
                                branchTargetOffset = (uint)operation.Value;
                            }

                            while (true) {
                                if (operations.ElementAt(branchTargetIdx).Offset == branchTargetOffset) {
                                    break;
                                }
                                else if (operations.ElementAt(branchTargetIdx).Offset > branchTargetOffset) {
                                    throw new ArgumentException("Could not find target off branch.");
                                }
                                branchTargetIdx++;
                            }

                            // create new exit branch object
                            ConditionalBranchTarget currentExitBranch = new ConditionalBranchTarget();
                            currentExitBranch.sourceBasicBlock = currentBasicBlock;
                            currentExitBranch.notTakenTarget = new BasicBlock(this.semanticId);
                            this.semanticId++;
                            currentExitBranch.takenTarget = new BasicBlock(this.semanticId);
                            this.semanticId++;
                            currentBasicBlock.exitBranch = currentExitBranch;

                            // start two new basic blocks from this branch on and end current execution
                            this.buildRecursivelyCfg(methodCfg, operations, idx + 1, currentExitBranch, ref currentExitBranch.notTakenTarget);
                            this.buildRecursivelyCfg(methodCfg, operations, branchTargetIdx, currentExitBranch, ref currentExitBranch.takenTarget);
                            return;
                        }

                    // unconditional branch instructions
                    case OperationCode.Br:
                    case OperationCode.Br_S: {

                            // the current basic block ends here
                            currentBasicBlock.endIdx = idx;

                            // calculate the target index of the branch
                            int branchTargetIdx = 0;
                            uint branchTargetOffset = (uint)operation.Value;
                            while (true) {
                                if (operations.ElementAt(branchTargetIdx).Offset == branchTargetOffset) {
                                    break;
                                }
                                else if (operations.ElementAt(branchTargetIdx).Offset > branchTargetOffset) {
                                    throw new ArgumentException("Could not find target off branch.");
                                }
                                branchTargetIdx++;
                            }

                            // create new exit branch object
                            UnconditionalBranchTarget currentExitBranch = new UnconditionalBranchTarget();
                            currentExitBranch.sourceBasicBlock = currentBasicBlock;
                            currentExitBranch.takenTarget = new BasicBlock(this.semanticId);
                            this.semanticId++;
                            currentBasicBlock.exitBranch = currentExitBranch;

                            // start one new basic block from this branch on and end current execution
                            this.buildRecursivelyCfg(methodCfg, operations, branchTargetIdx, currentExitBranch, ref currentExitBranch.takenTarget);
                            return;
                        }

                    // exit operation
                    case OperationCode.Ret: {

                            // the current basic block ends here
                            currentBasicBlock.endIdx = idx;

                            // create new exit branch object
                            ExitBranchTarget currentExitBranch = new ExitBranchTarget();
                            currentExitBranch.sourceBasicBlock = currentBasicBlock;
                            currentBasicBlock.exitBranch = currentExitBranch;

                            // end current execution
                            return;
                        }

                    // operations that exit the current function/control flow
                    case OperationCode.Throw: {

                            // the current basic block ends here
                            currentBasicBlock.endIdx = idx;

                            // create new exit branch object
                            ThrowBranchTarget currentExitBranch = new ThrowBranchTarget();
                            currentExitBranch.sourceBasicBlock = currentBasicBlock;
                            currentBasicBlock.exitBranch = currentExitBranch;

                            // start a new basic block if this was not the last instruction of the method
                            // (needed because some control flows that are reached via throw are not found without it) 
                            if ((idx + 1) < operations.Count()) {
                                BasicBlock newStartBasicBlock = new BasicBlock(this.semanticId);
                                this.semanticId++;
                                this.buildRecursivelyCfg(methodCfg, operations, idx + 1, null, ref newStartBasicBlock);
                            }

                            // end current execution
                            return;
                        }

                    // switch instruction (has a variable set of jump targets)
                    case OperationCode.Switch: {

                            // the current basic block ends here
                            currentBasicBlock.endIdx = idx;

                            // create new exit branch object
                            SwitchBranchTarget currentExitBranch = new SwitchBranchTarget();
                            currentExitBranch.sourceBasicBlock = currentBasicBlock;
                            currentExitBranch.notTakenTarget = new BasicBlock(this.semanticId);
                            this.semanticId++;
                            currentBasicBlock.exitBranch = currentExitBranch;

                            // calculate the target index of all switch branches
                            int counter = 0;
                            foreach (uint branchTargetOffset in (uint[])operation.Value) {
                                int branchTargetIdx = 0;
                                while (true) {
                                    if (operations.ElementAt(branchTargetIdx).Offset == branchTargetOffset) {
                                        break;
                                    }
                                    else if (operations.ElementAt(branchTargetIdx).Offset > branchTargetOffset) {
                                        throw new ArgumentException("Could not find target off branch.");
                                    }
                                    branchTargetIdx++;
                                }

                                // start a new basic block from this branch on
                                BasicBlock tempNextBasicBlock = new BasicBlock(this.semanticId);
                                this.semanticId++;
                                this.buildRecursivelyCfg(methodCfg, operations, branchTargetIdx, currentExitBranch, ref tempNextBasicBlock);

                                // add new basic block to branch targets
                                currentExitBranch.takenTarget.Add(tempNextBasicBlock);

                                counter++;
                            }

                            // start a new basic block directly after the switch instruction and end current execution
                            this.buildRecursivelyCfg(methodCfg, operations, idx + 1, currentExitBranch, ref currentExitBranch.notTakenTarget);

                            return;
                        }

                    // exception handler beginning or end (end of try block or catch block)
                    case OperationCode.Leave:
                    case OperationCode.Leave_S: {

                            // the current basic block ends here
                            currentBasicBlock.endIdx = idx;

                            // calculate the target index of the branch
                            int branchTargetIdx = 0;
                            uint branchTargetOffset = (uint)operation.Value;
                            while (true) {
                                if (operations.ElementAt(branchTargetIdx).Offset == branchTargetOffset) {
                                    break;
                                }
                                else if (operations.ElementAt(branchTargetIdx).Offset > branchTargetOffset) {
                                    throw new ArgumentException("Could not find target off branch.");
                                }
                                branchTargetIdx++;
                            }

                            // create new exit branch object
                            ExceptionBranchTarget currentExitBranch = new ExceptionBranchTarget();
                            currentExitBranch.sourceBasicBlock = currentBasicBlock;
                            currentExitBranch.exceptionTarget = new BasicBlock(this.semanticId);
                            this.semanticId++;
                            currentExitBranch.exitTarget = new BasicBlock(this.semanticId);
                            this.semanticId++;
                            currentBasicBlock.exitBranch = currentExitBranch;

                            // start two new basic blocks from this branch on and end current execution
                            this.buildRecursivelyCfg(methodCfg, operations, idx + 1, currentExitBranch, ref currentExitBranch.exceptionTarget);
                            this.buildRecursivelyCfg(methodCfg, operations, branchTargetIdx, currentExitBranch, ref currentExitBranch.exitTarget);
                            return;
                        }

                    // create a virtual basic block at the end of a catch/finally handler
                    case OperationCode.Rethrow:
                    case OperationCode.Endfinally: {

                            // the current basic block ends here
                            currentBasicBlock.endIdx = idx;

                            // create new exit branch object
                            NoBranchTarget currentExitBranch = new NoBranchTarget();
                            currentExitBranch.sourceBasicBlock = currentBasicBlock;
                            currentExitBranch.takenTarget = new BasicBlock(this.semanticId);
                            this.semanticId++;
                            currentBasicBlock.exitBranch = currentExitBranch;

                            // start a new basic block from this branch on and end current execution
                            this.buildRecursivelyCfg(methodCfg, operations, idx + 1, currentExitBranch, ref currentExitBranch.takenTarget);

                            return;
                        }


                    default:
                        break;
                }
            }
        }


        // builds a CFG for the given method and returns it
        public MethodCfg buildCfgForMethod(MethodDefinition method) {

            MethodCfg methodCfg = new MethodCfg();
            methodCfg.method = method;

            // start building the cfg
            BasicBlock firstBasicBlock = new BasicBlock(this.semanticId);
            this.semanticId++;
            methodCfg.startBasicBlock = firstBasicBlock;
            this.buildRecursivelyCfg(methodCfg, method.Body.Operations, 0, null, ref firstBasicBlock);

            // add exception handler to the cfg
            foreach (IOperationExceptionInformation exceptionHandler in method.Body.OperationExceptionInformation) {

                // debugging variables to throw an exception if start/end of try/handler block was not found
                bool tryStartFound = false;
                bool tryEndFound = false;
                bool handlerStartFound = false;
                bool handlerEndFound = false;

                // search each basic block if they reside in an exception handler
                List<BasicBlock> localCopy = new List<BasicBlock>();
                localCopy.AddRange(methodCfg.basicBlocks);
                for (int bbIdx = 0; bbIdx < localCopy.Count(); bbIdx++) {
                    BasicBlock basicBlock = localCopy.ElementAt(bbIdx);
                    int lastOperationIdx = basicBlock.operations.Count() - 1;

                    // ignore basic block if the start of the try block lies behind the current basic block
                    // (catch or finally blocks can never lie before a try block)
                    if (basicBlock.operations.ElementAt(lastOperationIdx).Offset < exceptionHandler.TryStartOffset) {
                        continue;
                    }

                    // check if the current basic block resides in the try block of the exception handler
                    else if ((exceptionHandler.TryStartOffset >= basicBlock.operations.ElementAt(0).Offset && exceptionHandler.TryStartOffset <= basicBlock.operations.ElementAt(lastOperationIdx).Offset)
                        || (exceptionHandler.TryStartOffset <= basicBlock.operations.ElementAt(0).Offset && basicBlock.operations.ElementAt(lastOperationIdx).Offset < exceptionHandler.TryEndOffset)) {

                        TryBlock tempTryBlock = new TryBlock();
                        tempTryBlock.exceptionHandler = exceptionHandler;

                        // check if the try block starts inside this basic block
                        if (exceptionHandler.TryStartOffset >= basicBlock.operations.ElementAt(0).Offset) {

                            // find the index of the operation that starts the try block
                            bool foundIdx = false;
                            for (int idx = 0; idx < basicBlock.operations.Count(); idx++) {
                                if (basicBlock.operations.ElementAt(idx).Offset == exceptionHandler.TryStartOffset) {
                                    
                                    // if the try block does not start at the beginning of the current basic block
                                    // => split basic block and let the try block begin at the second half of the basic block
                                    if (idx != 0) {

                                        // create new basic block which will be the second half of the found one
                                        BasicBlock newBasicBlock = new BasicBlock(this.semanticId);
                                        this.semanticId++;
                                        newBasicBlock.startIdx = basicBlock.startIdx + idx;
                                        newBasicBlock.endIdx = basicBlock.endIdx;
                                        basicBlock.endIdx = basicBlock.startIdx + idx - 1;

                                        // move the exit basic blocks to the new created basic block
                                        // and make the new created basic block the exit of the splitted one   
                                        newBasicBlock.exitBranch = basicBlock.exitBranch;
                                        newBasicBlock.exitBranch.sourceBasicBlock = newBasicBlock;
                                        TryBlockTarget tempExitBranch = new TryBlockTarget();
                                        tempExitBranch.takenTarget = newBasicBlock;
                                        tempExitBranch.sourceBasicBlock = basicBlock;
                                        basicBlock.exitBranch = tempExitBranch;

                                        // add splitted basic block branch to the entry of the new one
                                        newBasicBlock.entryBranches.Add(tempExitBranch);

                                        // distribute the instructions to the basic blocks 
                                        List<IOperation> previousOperations = new List<IOperation>();
                                        List<IOperation> nextOperations = new List<IOperation>();
                                        for (int operationIdx = 0; (operationIdx + basicBlock.startIdx) <= newBasicBlock.endIdx; operationIdx++) {

                                            if ((operationIdx + basicBlock.startIdx) < newBasicBlock.startIdx) {
                                                previousOperations.Add(basicBlock.operations.ElementAt(operationIdx));
                                            }
                                            else {
                                                nextOperations.Add(basicBlock.operations.ElementAt(operationIdx));
                                            }
                                        }
                                        basicBlock.operations = previousOperations;
                                        newBasicBlock.operations = nextOperations;

                                        // add new basic block to the cfg
                                        methodCfg.basicBlocks.Add(newBasicBlock);

                                        // add try block to the new created basic block (it is the start of the try block)
                                        newBasicBlock.tryBlocks.Add(tempTryBlock);

                                        // try block can not end at the first part of the splitted basic block
                                        // (because it starts at the second one), but it can end at the
                                        // second part of the splitted basic block
                                        basicBlock = newBasicBlock;
                                        lastOperationIdx = newBasicBlock.operations.Count() - 1;
                                    }

                                    // the try block starts at the beginning of the current basic block => add the try block
                                    else {
                                        basicBlock.tryBlocks.Add(tempTryBlock);
                                    }
                                    
                                    // mark try block as the beginning of the try block
                                    tempTryBlock.firstBasicBlockOfTryBlock = true;
                                    foundIdx = true;
                                    tryStartFound = true;
                                    break;
                                }
                            }
                            if (!foundIdx) {
                                throw new ArgumentException("Did not find index of operation that starts Try block.");
                            }
                        }
                        else {
                            basicBlock.tryBlocks.Add(tempTryBlock);
                            tempTryBlock.firstBasicBlockOfTryBlock = false;
                        }

                        // check if the try block ends at the end of this basic block (try blocks always end at the end of a basic block)
                        if (basicBlock.operations.ElementAt(lastOperationIdx).Offset + CfgBuilder.getSizeOfOperation(basicBlock.operations.ElementAt(lastOperationIdx)) == exceptionHandler.TryEndOffset) {
                            tempTryBlock.lastBasicBlockOfTryBlock = true;
                            tryEndFound = true;
                        }
                        else {
                            tempTryBlock.lastBasicBlockOfTryBlock = false;
                        }

                        continue;
                    }

                    // check if the current basic block resides in the handler block of the exception handler
                    else if ((exceptionHandler.HandlerStartOffset >= basicBlock.operations.ElementAt(0).Offset && exceptionHandler.HandlerStartOffset <= basicBlock.operations.ElementAt(lastOperationIdx).Offset)
                        || (exceptionHandler.HandlerStartOffset <= basicBlock.operations.ElementAt(0).Offset && basicBlock.operations.ElementAt(lastOperationIdx).Offset < exceptionHandler.HandlerEndOffset)) {

                        // check if the handler block is a catch block
                        if (exceptionHandler.HandlerKind == HandlerKind.Catch) {
                            HandlerBlock tempCatchBock = new HandlerBlock();
                            tempCatchBock.typeOfHandler = HandlerKind.Catch;
                            tempCatchBock.exceptionHandler = exceptionHandler;
                            basicBlock.handlerBlocks.Add(tempCatchBock);

                            // check if the catch block starts inside this basic block
                            if (exceptionHandler.HandlerStartOffset >= basicBlock.operations.ElementAt(0).Offset) {

                                // find the index of the operation that starts the catch block
                                bool foundIdx = false;
                                for (int idx = 0; idx < basicBlock.operations.Count(); idx++) {
                                    if (basicBlock.operations.ElementAt(idx).Offset == exceptionHandler.HandlerStartOffset) {
                                        tempCatchBock.firstBasicBlockOfHandlerBlock = true;
                                        foundIdx = true;
                                        handlerStartFound = true;
                                        break;
                                    }
                                }
                                if (!foundIdx) {
                                    throw new ArgumentException("Did not find index of operation that starts Catch block.");
                                }
                            }
                            else {
                                tempCatchBock.firstBasicBlockOfHandlerBlock = false;
                            }

                            // check if the catch block ends inside this basic block
                            if (basicBlock.operations[0].Offset < exceptionHandler.HandlerEndOffset
                                && exceptionHandler.HandlerEndOffset <= (basicBlock.operations.ElementAt(lastOperationIdx).Offset + CfgBuilder.getSizeOfOperation(basicBlock.operations.ElementAt(lastOperationIdx)))) {

                                // check if the catch block ends at the last instruction of this basic block (this happens usually => optimization)
                                if (basicBlock.operations.ElementAt(lastOperationIdx).Offset + CfgBuilder.getSizeOfOperation(basicBlock.operations.ElementAt(lastOperationIdx)) == exceptionHandler.HandlerEndOffset) {
                                    tempCatchBock.lastBasicBlockOfHandlerBlock = true;
                                    handlerEndFound = true;
                                    continue;
                                }
                                else {
                                    throw new ArgumentException("Did not find the operation that ends Catch block.");
                                }
                            }
                            else {
                                tempCatchBock.lastBasicBlockOfHandlerBlock = false;
                            }

                            continue;
                        }

                            // check if the handler block is a finally block
                        else if (exceptionHandler.HandlerKind == HandlerKind.Finally) {
                            HandlerBlock tempFinallyBock = new HandlerBlock();
                            tempFinallyBock.typeOfHandler = HandlerKind.Finally;
                            tempFinallyBock.exceptionHandler = exceptionHandler;
                            basicBlock.handlerBlocks.Add(tempFinallyBock);

                            // check if the finally block starts inside this basic block
                            if (exceptionHandler.HandlerStartOffset >= basicBlock.operations.ElementAt(0).Offset) {

                                // find the index of the operation that starts the finally block
                                bool foundIdx = false;
                                for (int idx = 0; idx < basicBlock.operations.Count(); idx++) {
                                    if (basicBlock.operations.ElementAt(idx).Offset == exceptionHandler.HandlerStartOffset) {
                                        tempFinallyBock.firstBasicBlockOfHandlerBlock = true;
                                        foundIdx = true;
                                        handlerStartFound = true;
                                        break;
                                    }
                                }
                                if (!foundIdx) {
                                    throw new ArgumentException("Did not find index of operation that starts Finally block.");
                                }
                            }
                            else {
                                tempFinallyBock.firstBasicBlockOfHandlerBlock = false;
                            }

                            // check if the finally block ends inside this basic block
                            if (basicBlock.operations[0].Offset < exceptionHandler.HandlerEndOffset
                                && exceptionHandler.HandlerEndOffset <= (basicBlock.operations.ElementAt(lastOperationIdx).Offset + CfgBuilder.getSizeOfOperation(basicBlock.operations.ElementAt(lastOperationIdx)))) {

                                // check if the finally block ends at the last instruction of this basic block
                                if (basicBlock.operations.ElementAt(lastOperationIdx).Offset + CfgBuilder.getSizeOfOperation(basicBlock.operations.ElementAt(lastOperationIdx)) == exceptionHandler.HandlerEndOffset) {
                                    tempFinallyBock.lastBasicBlockOfHandlerBlock = true;
                                    handlerEndFound = true;
                                    continue;
                                }
                                else {
                                    throw new ArgumentException("Did not find the operation that ends Finally block.");
                                }
                            }

                            continue;
                        }

                        else {
                            throw new ArgumentException("Do not know how to handle exception handler.");
                        }
                    }
                }

                // check if start/end of try/handler block was found
                if(!tryStartFound
                    || !tryEndFound
                    || !handlerStartFound
                    || !handlerEndFound) {
                    throw new ArgumentException("Was not able to find start/end of try/handler block.");
                }

            }



            // TODO
            // DEBUG
            //this.logger.dumpMethodCfg(methodCfg, this.logger.sanitizeString(methodCfg.method.ToString()));

            return methodCfg;
        }


        // builds a CFG for the given class (means a CFG for all methods of this class) and returns it
        public ClassCfg buildCfgForClass(NamespaceTypeDefinition targetClass) {

            ClassCfg classCfg = new ClassCfg();
            classCfg.classObj = targetClass;

            if (targetClass.Methods != null) {
                foreach (MethodDefinition method in targetClass.Methods) {

                    // ignore method if it is external
                    if (method.IsExternal) {
                        continue;
                    }

                    logger.writeLine("Create CFG for method \"" + method.ToString() + "\"");
                    classCfg.methodCfgs.Add(buildCfgForMethod(method));
                    logger.writeLine("");
                }
            }

            return classCfg;
        }


        // builds recursively a basic block union (a list of basic blocks that belong together in the control flow because
        // they are lying directly behind each other)
        private void createRecursivelyBasicBlockUnion(IList<BasicBlockUnion> basicBlockUnions, BasicBlockUnion currentUnion, BasicBlock currentBasicBlock) {

            // check if current basic block already belongs to a basic block union
            foreach (BasicBlockUnion basicBlockUnion in basicBlockUnions) {
                // ignore current basic block union
                if (basicBlockUnion == currentUnion) {
                    continue;
                }
                foreach (BasicBlock tempBB in basicBlockUnion.basicBlocks) {
                    if (tempBB == currentBasicBlock) {
                        return;
                    }
                }
            }

            // if there are no basic blocks inside the current basic block union
            // check if the entry to the current basic block could be a not taken branch
            // or no branch at all => start building the basic block union from the predecessor basic block
            if (currentUnion.basicBlocks.Count() == 0) {
                foreach (IBranchTarget entryBranch in currentBasicBlock.entryBranches) {
                    BasicBlock predecessorBasicBlock = entryBranch.sourceBasicBlock;

                    if (predecessorBasicBlock.exitBranch as NoBranchTarget != null) {
                        NoBranchTarget predecessorExitBranch = (predecessorBasicBlock.exitBranch as NoBranchTarget);

                        // check if the basic block that comes directly after the predecessor basic block is the current one
                        // => start building basic block union from the predecessor
                        if (predecessorExitBranch.takenTarget == currentBasicBlock) {
                            this.createRecursivelyBasicBlockUnion(basicBlockUnions, currentUnion, predecessorBasicBlock);
                            return;
                        }

                        continue;
                    }
                    else if (predecessorBasicBlock.exitBranch as TryBlockTarget != null) {
                        TryBlockTarget predecessorExitBranch = (predecessorBasicBlock.exitBranch as TryBlockTarget);

                        // check if the basic block that comes directly after the predecessor basic block is the current one
                        // => start building basic block union from the predecessor
                        if (predecessorExitBranch.takenTarget == currentBasicBlock) {
                            this.createRecursivelyBasicBlockUnion(basicBlockUnions, currentUnion, predecessorBasicBlock);
                            return;
                        }

                        continue;                        
                    }
                    else if (predecessorBasicBlock.exitBranch as ConditionalBranchTarget != null) {
                        ConditionalBranchTarget predecessorExitBranch = (predecessorBasicBlock.exitBranch as ConditionalBranchTarget);

                        // check if the basic block that comes directly after the predecessor basic block is the current one
                        // => start building basic block union from the predecessor
                        if (predecessorExitBranch.notTakenTarget == currentBasicBlock) {
                            this.createRecursivelyBasicBlockUnion(basicBlockUnions, currentUnion, predecessorBasicBlock);
                            return;
                        }

                        continue;
                    }
                    else if (predecessorBasicBlock.exitBranch as SwitchBranchTarget != null) {
                        SwitchBranchTarget predecessorExitBranch = (predecessorBasicBlock.exitBranch as SwitchBranchTarget);

                        // check if the basic block that comes directly after the predecessor basic block is the current one
                        // => start building basic block union from the predecessor
                        if (predecessorExitBranch.notTakenTarget == currentBasicBlock) {
                            this.createRecursivelyBasicBlockUnion(basicBlockUnions, currentUnion, predecessorBasicBlock);
                            return;
                        }

                        continue;
                    }
                    else if (predecessorBasicBlock.exitBranch as ExceptionBranchTarget != null) {
                        ExceptionBranchTarget predecessorExitBranch = (predecessorBasicBlock.exitBranch as ExceptionBranchTarget);

                        // check if the basic block that comes directly after the predecessor basic block is the current one
                        // => start building basic block union from the predecessor
                        if (predecessorExitBranch.exceptionTarget == currentBasicBlock) {
                            this.createRecursivelyBasicBlockUnion(basicBlockUnions, currentUnion, predecessorBasicBlock);
                            return;
                        }

                        continue;
                    }
                }
            }

            // add current basic block to the union
            currentUnion.basicBlocks.Add(currentBasicBlock);

            // follow all not taken branches and add them to the current basic block union
            // (this means all basic blocks that follow directly the current one directly
            // because they lie directly behind the current one are added to the basic block union)
            if (currentBasicBlock.exitBranch as NoBranchTarget != null) {
                NoBranchTarget tempExitBranch = (currentBasicBlock.exitBranch as NoBranchTarget);
                this.createRecursivelyBasicBlockUnion(basicBlockUnions, currentUnion, tempExitBranch.takenTarget);
                return;
            }
            else if (currentBasicBlock.exitBranch as TryBlockTarget != null) {
                TryBlockTarget tempExitBranch = (currentBasicBlock.exitBranch as TryBlockTarget);
                this.createRecursivelyBasicBlockUnion(basicBlockUnions, currentUnion, tempExitBranch.takenTarget);
                return;
            }
            else if (currentBasicBlock.exitBranch as ConditionalBranchTarget != null) {
                ConditionalBranchTarget tempExitBranch = (currentBasicBlock.exitBranch as ConditionalBranchTarget);
                this.createRecursivelyBasicBlockUnion(basicBlockUnions, currentUnion, tempExitBranch.notTakenTarget);
                return;
            }
            else if (currentBasicBlock.exitBranch as SwitchBranchTarget != null) {
                SwitchBranchTarget tempExitBranch = (currentBasicBlock.exitBranch as SwitchBranchTarget);
                this.createRecursivelyBasicBlockUnion(basicBlockUnions, currentUnion, tempExitBranch.notTakenTarget);
                return;
            }
            else if (currentBasicBlock.exitBranch as UnconditionalBranchTarget != null) {
                return;
            }
            else if (currentBasicBlock.exitBranch as ExitBranchTarget != null) {
                return;
            }
            else if (currentBasicBlock.exitBranch as ThrowBranchTarget != null) {
                return;
            }
            else if (currentBasicBlock.exitBranch as ExceptionBranchTarget != null) {
                ExceptionBranchTarget tempExitBranch = (currentBasicBlock.exitBranch as ExceptionBranchTarget);
                this.createRecursivelyBasicBlockUnion(basicBlockUnions, currentUnion, tempExitBranch.exceptionTarget);
                return;
            }
            else {
                throw new ArgumentException("Do not know how to handle exit branch.");
            }
        }


        // creates a class of the given class CFG (IMPORTANT: it means that it
        // updates the existing class, not creating a new one)
        public void createClassFromCfg(ClassCfg classCfg) {

            NamespaceTypeDefinition targetClass = classCfg.classObj;

            foreach (MethodCfg methodCfg in classCfg.methodCfgs) {

                // ignore method if it is external
                if (methodCfg.method.IsExternal) {
                    continue;
                }

                logger.writeLine("Create method from CFG for \"" + methodCfg.method.ToString() + "\"");
                createMethodFromCfg(methodCfg);
                logger.writeLine("");
            }
        }


        // creates a method for a given method CFG (IMPORTANT: it updates the operations
        // and exception handlers of the existing method, does not create a new method)
        public void createMethodFromCfg(MethodCfg methodCfg) {

            // list of basic blocks that belong together logically (i.e. BB2 lies directly behind BB1 because it reaches it without a branch)
            IList<BasicBlockUnion> basicBlockUnions = new List<BasicBlockUnion>();

            // first step: create basic block unions
            // create first basic block union starting from the start basic block
            BasicBlockUnion firstBasicBlockUnion = new BasicBlockUnion();
            this.createRecursivelyBasicBlockUnion(basicBlockUnions, firstBasicBlockUnion, methodCfg.startBasicBlock);
            basicBlockUnions.Add(firstBasicBlockUnion);

            // create basic block unions for the rest of the basic blocks
            foreach (BasicBlock tempBB in methodCfg.basicBlocks) {

                // create a basic block union (if basic block is not contained by another existing basic block union)
                BasicBlockUnion basicBlockUnion = new BasicBlockUnion();
                this.createRecursivelyBasicBlockUnion(basicBlockUnions, basicBlockUnion, tempBB);

                // if the newly created basic block union contains basic blocks
                // => add to list of basic block unions
                if (basicBlockUnion.basicBlocks.Count() != 0) {
                    basicBlockUnions.Add(basicBlockUnion);
                }
            }


            // step two: merge basic block unions with respect to the exception handler
            foreach (OperationExceptionInformation exceptionHandler in methodCfg.method.Body.OperationExceptionInformation) {

                // create a temporary object to reorder the basic block unions with respect to the exception handler
                BasicBlockUnionExceptionHandlerOrder tempNewOrder = new BasicBlockUnionExceptionHandlerOrder();
                tempNewOrder.exceptionHandler = exceptionHandler;

                // find all basic blocks that are connected to the current exception handler
                foreach (BasicBlockUnion basicBlockUnion in basicBlockUnions) {
                    foreach (BasicBlock basicBlock in basicBlockUnion.basicBlocks) {

                        // ignore all basic blocks that do not have any try/catch/finally handler in it
                        if (basicBlock.tryBlocks.Count() == 0 && basicBlock.handlerBlocks.Count() == 0) {
                            continue;
                        }

                        // find the try block that is connected to the current exception handler
                        foreach (TryBlock tryBlock in basicBlock.tryBlocks) {

                            // ignore try blocks that do not belong to the current exception handler
                            if (tryBlock.exceptionHandler != exceptionHandler) {
                                continue;
                            }

                            // check if basic block union already resides in try body
                            // if not => add to try body
                            if (!tempNewOrder.tryBody.Contains(basicBlockUnion)) {
                                tempNewOrder.tryBody.Add(basicBlockUnion);
                            }

                            // check if the basic block starts the try block
                            if (tryBlock.firstBasicBlockOfTryBlock) {
                                tempNewOrder.tryStart = basicBlockUnion;
                            }

                            // check if the basic block ends the try block
                            if (tryBlock.lastBasicBlockOfTryBlock) {
                                tempNewOrder.tryEnd = basicBlockUnion;
                            }
                        }

                        // find the handler block that is connected to the current exception handler
                        foreach (HandlerBlock handlerBlock in basicBlock.handlerBlocks) {

                            // ignore handler blocks that do not belong to the current exception handler
                            if (handlerBlock.exceptionHandler != exceptionHandler) {
                                continue;
                            }

                            // check if basic block union already resides in handler body
                            // if not => add to handler body
                            if (!tempNewOrder.handlerBody.Contains(basicBlockUnion)) {
                                tempNewOrder.handlerBody.Add(basicBlockUnion);
                            }

                            // check if the basic block starts the handler block
                            if (handlerBlock.firstBasicBlockOfHandlerBlock) {
                                tempNewOrder.handlerStart = basicBlockUnion;
                            }

                            // check if the basic block ends the handler block
                            if (handlerBlock.lastBasicBlockOfHandlerBlock) {
                                tempNewOrder.handlerEnd = basicBlockUnion;
                            }
                        }
                    }
                }
                if (tempNewOrder.tryStart == null
                    || tempNewOrder.tryEnd == null
                    || tempNewOrder.handlerStart == null
                    || tempNewOrder.handlerEnd == null) {
                        throw new ArgumentException("Could not find all try/catch beginnings/ends.");
                }



                // merge basic block unions with respect to the current exception handler

                // check if the complete exception handling is done in only one basic block union
                // => ignore it
                if (tempNewOrder.tryStart == tempNewOrder.tryEnd
                    && tempNewOrder.handlerStart == tempNewOrder.handlerEnd
                    && tempNewOrder.tryStart == tempNewOrder.handlerStart) {
                    continue;
                }

                // if not everything lies in the same basic block union
                // extend the basic block union that starts the try block
                BasicBlockUnion extendedUnion = tempNewOrder.tryStart;

                // if try block start and end are the same => nothing to merge
                // else merge the basic block unions
                if (tempNewOrder.tryStart != tempNewOrder.tryEnd) {
                    foreach (BasicBlockUnion tryBodyBasicBlockUnion in tempNewOrder.tryBody) {

                        // ignore the basic block union that starts the try block (already in extended union)
                        // and ignore the basic block union that ends the try block (is added last)
                        if (tryBodyBasicBlockUnion == tempNewOrder.tryStart
                            || tryBodyBasicBlockUnion == tempNewOrder.tryEnd) {
                            continue;
                        }

                        // extend first basic block union with the basic block union of the
                        // try block body and remove the added one from the list of basic block unions
                        extendedUnion.basicBlocks.AddRange(tryBodyBasicBlockUnion.basicBlocks);
                        basicBlockUnions.Remove(tryBodyBasicBlockUnion);
                    }

                    // extend first basic block union with the basic block union of the
                    // try block end and remove the added one from the list of basic block unions
                    extendedUnion.basicBlocks.AddRange(tempNewOrder.tryEnd.basicBlocks);
                    basicBlockUnions.Remove(tempNewOrder.tryEnd);
                }

                // if the try block end and the handler block start are not the same basic block union
                // => merge the union of the handler block start to the current basic block union
                if (tempNewOrder.tryEnd != tempNewOrder.handlerStart) {
                    extendedUnion.basicBlocks.AddRange(tempNewOrder.handlerStart.basicBlocks);
                    basicBlockUnions.Remove(tempNewOrder.handlerStart);
                }

                // if handler block start and end are the same => nothing to merge
                // else merge the basic block unions
                if (tempNewOrder.handlerStart != tempNewOrder.handlerEnd) {
                    foreach (BasicBlockUnion handlerBodyBasicBlockUnion in tempNewOrder.handlerBody) {

                        // ignore the basic block union that starts the handler block (already in extended union)
                        // and ignore the basic block union that ends the handler block (is added last)
                        if (handlerBodyBasicBlockUnion == tempNewOrder.handlerStart
                            || handlerBodyBasicBlockUnion == tempNewOrder.handlerEnd) {
                            continue;
                        }

                        // add basic block union of the handler block body to the extended basic block union 
                        // and remove the added one from the list of basic block unions
                        extendedUnion.basicBlocks.AddRange(handlerBodyBasicBlockUnion.basicBlocks);
                        basicBlockUnions.Remove(handlerBodyBasicBlockUnion);
                    }

                    // extend the extended basic block union with the basic block union of the
                    // handler block end and remove the added one from the list of basic block unions
                    extendedUnion.basicBlocks.AddRange(tempNewOrder.handlerEnd.basicBlocks);
                    basicBlockUnions.Remove(tempNewOrder.handlerEnd);
                }
            }


            // step three: create one list of operations and modify jump offsets
            // create a list of operations from the basic blocks,
            // update basic block start/end indices accordingly
            // and update the offsets of the operations (needed to update branch operations)
            IList<IOperation> methodOperations = new List<IOperation>();
            int operationIdx = 0;
            uint currentOffset = 0;
            foreach (BasicBlockUnion basicBlockUnion in basicBlockUnions) {
                foreach (BasicBlock basicBlock in basicBlockUnion.basicBlocks) {
                    basicBlock.startIdx = operationIdx;
                    //foreach (IOperation operation in basicBlock.operations) {
                    for (int opIdx = 0; opIdx < basicBlock.operations.Count(); opIdx++) {
                        IOperation operation = basicBlock.operations.ElementAt(opIdx);

                        // a new operation object is needed because the old operations
                        // are read only due to cci
                        InternalOperation newOperation = new InternalOperation();

                        // exchange every short instruction with its larger one
                        switch (operation.OperationCode) {
                            case OperationCode.Beq_S:
                                newOperation.OperationCode = OperationCode.Beq;
                                break;
                            case OperationCode.Bge_S:
                                newOperation.OperationCode = OperationCode.Bge;
                                break;
                            case OperationCode.Bge_Un_S:
                                newOperation.OperationCode = OperationCode.Bge_Un;
                                break;
                            case OperationCode.Bgt_S:
                                newOperation.OperationCode = OperationCode.Bgt;
                                break;
                            case OperationCode.Bgt_Un_S:
                                newOperation.OperationCode = OperationCode.Bgt_Un;
                                break;
                            case OperationCode.Ble_S:
                                newOperation.OperationCode = OperationCode.Ble;
                                break;
                            case OperationCode.Ble_Un_S:
                                newOperation.OperationCode = OperationCode.Ble_Un;
                                break;
                            case OperationCode.Blt_S:
                                newOperation.OperationCode = OperationCode.Blt;
                                break;
                            case OperationCode.Blt_Un_S:
                                newOperation.OperationCode = OperationCode.Blt_Un;
                                break;
                            case OperationCode.Bne_Un_S:
                                newOperation.OperationCode = OperationCode.Bne_Un;
                                break;
                            case OperationCode.Br_S:
                                newOperation.OperationCode = OperationCode.Br;
                                break;
                            case OperationCode.Brfalse_S:
                                newOperation.OperationCode = OperationCode.Brfalse;
                                break;
                            case OperationCode.Brtrue_S:
                                newOperation.OperationCode = OperationCode.Brtrue;
                                break;
                            case OperationCode.Ldarg_S:
                                newOperation.OperationCode = OperationCode.Ldarg;
                                break;
                            case OperationCode.Ldarga_S:
                                newOperation.OperationCode = OperationCode.Ldarga;
                                break;
                            case OperationCode.Ldc_I4_S:
                                newOperation.OperationCode = OperationCode.Ldc_I4;
                                break;
                            case OperationCode.Ldloc_S:
                                newOperation.OperationCode = OperationCode.Ldloc;
                                break;
                            case OperationCode.Ldloca_S:
                                newOperation.OperationCode = OperationCode.Ldloca;
                                break;
                            case OperationCode.Leave_S:
                                newOperation.OperationCode = OperationCode.Leave;
                                break;
                            case OperationCode.Starg_S:
                                newOperation.OperationCode = OperationCode.Starg;
                                break;
                            case OperationCode.Stloc_S:
                                newOperation.OperationCode = OperationCode.Stloc;
                                break;
                            default:
                                newOperation.OperationCode = operation.OperationCode;
                                break;
                        }
                        newOperation.Value = operation.Value;
                        newOperation.Location = operation.Location;
                        newOperation.Offset = currentOffset;
                        methodOperations.Add(newOperation);

                        // replace old operation in basic block with newly created one
                        basicBlock.operations[opIdx] = newOperation;

                        operationIdx++;
                        currentOffset += CfgBuilder.getSizeOfOperation(newOperation);
                    }
                    basicBlock.endIdx = operationIdx - 1;
                }
            }


            // step four: update branches with new target offsets
            foreach (BasicBlock tempBB in methodCfg.basicBlocks) {
                InternalOperation branchOperation = (InternalOperation)methodOperations.ElementAt(tempBB.endIdx);

                // check which kind of exit branch is used by this basic block and update its operation accordingly
                if (tempBB.exitBranch as NoBranchTarget != null) {
                    continue;
                }
                else if (tempBB.exitBranch as TryBlockTarget != null) {
                    continue;
                }
                else if (tempBB.exitBranch as ConditionalBranchTarget != null) {
                    ConditionalBranchTarget tempExitBranch = (tempBB.exitBranch as ConditionalBranchTarget);

                    // check if the branch is done by a branch operation
                    if (!CfgBuilder.isBranchOperation(branchOperation)) {
                        throw new ArgumentException("Branch is not done by a valid branch operation.");
                    }

                    // get index of the branch target
                    int targetBranchIdx = tempExitBranch.takenTarget.startIdx;

                    // update offset of the taken branch
                    branchOperation.Value = methodOperations.ElementAt(targetBranchIdx).Offset;
                }
                else if (tempBB.exitBranch as SwitchBranchTarget != null) {
                    SwitchBranchTarget tempExitBranch = (tempBB.exitBranch as SwitchBranchTarget);

                    // check if the branch is done by a branch operation
                    if (!CfgBuilder.isBranchOperation(branchOperation)) {
                        throw new ArgumentException("Branch is not done by a valid branch operation.");
                    }

                    // update all switch branches
                    for (int switchIdx = 0; switchIdx < tempExitBranch.takenTarget.Count(); switchIdx++) {

                        // get index of the branch target
                        int targetBranchIdx = tempExitBranch.takenTarget.ElementAt(switchIdx).startIdx;

                        // update offset of the taken branch
                        ((uint[])branchOperation.Value)[switchIdx] = methodOperations.ElementAt(targetBranchIdx).Offset;
                    }
                }
                else if (tempBB.exitBranch as UnconditionalBranchTarget != null) {
                    UnconditionalBranchTarget tempExitBranch = (tempBB.exitBranch as UnconditionalBranchTarget);

                    // check if the branch is done by a branch operation
                    if (!CfgBuilder.isBranchOperation(branchOperation)) {
                        throw new ArgumentException("Branch is not done by a valid branch operation.");
                    }

                    // get index of the branch target
                    int targetBranchIdx = tempExitBranch.takenTarget.startIdx;

                    // update offset of the taken branch
                    branchOperation.Value = methodOperations.ElementAt(targetBranchIdx).Offset;
                }
                else if (tempBB.exitBranch as ExitBranchTarget != null) {
                    continue;
                }
                else if (tempBB.exitBranch as ThrowBranchTarget != null) {
                    continue;
                }
                else if (tempBB.exitBranch as ExceptionBranchTarget != null) {
                    ExceptionBranchTarget tempExitBranch = (tempBB.exitBranch as ExceptionBranchTarget);

                    // check if the branch is done by a branch operation
                    if (!CfgBuilder.isBranchOperation(branchOperation)) {
                        throw new ArgumentException("Branch is not done by a valid branch operation.");
                    }

                    // get index of the branch target
                    int targetBranchIdx = tempExitBranch.exitTarget.startIdx;

                    // update offset of the taken branch
                    branchOperation.Value = methodOperations.ElementAt(targetBranchIdx).Offset;
                }
                else {
                    throw new ArgumentException("Do not know how to handle exit branch.");
                }
            }


            // step five: create new exception handler with updated offsets and
            // create new method body of the cci method
            MethodDefinition method = methodCfg.method;
            var ilGenerator = new ILGenerator(this.host, method);
            // emit all operations to the new body
            foreach (IOperation operation in methodOperations) {
                ilGenerator.Emit(operation.OperationCode, operation.Value);
            }

            // list that is used to replace the pointer to the old exception handler in the basic blocks to the new exception handler
            List<OldExceptionHandlerMapping> oldExceptionHandlerMappings = new List<OldExceptionHandlerMapping>();
            
            // create for each old exception handler a new one with updated data
            foreach (IOperationExceptionInformation exceptionHandler in method.Body.OperationExceptionInformation) {

                // create mapping object for old exception handler
                OldExceptionHandlerMapping oldExceptionHandlerMapping = new OldExceptionHandlerMapping();
                oldExceptionHandlerMapping.oldExceptionHandler = exceptionHandler;
                oldExceptionHandlerMappings.Add(oldExceptionHandlerMapping);

                ILGeneratorLabel tryStart = new ILGeneratorLabel();
                ILGeneratorLabel tryEnd = new ILGeneratorLabel();
                ILGeneratorLabel handlerStart = new ILGeneratorLabel();
                ILGeneratorLabel handlerEnd = new ILGeneratorLabel();
                ILGeneratorLabel filterStart = new ILGeneratorLabel();

                // search for the basic blocks that start/end the try block and handler block
                foreach (BasicBlock tempBB in methodCfg.basicBlocks) {

                    foreach (TryBlock tempTryBlock in tempBB.tryBlocks) {

                        // ignore try blocks that do not belong to the current exception handler
                        if (tempTryBlock.exceptionHandler != exceptionHandler) {
                            continue;
                        }

                        // get offset of the instruction that starts the try block
                        if (tempTryBlock.firstBasicBlockOfTryBlock) {
                            tryStart.Offset = tempBB.operations.ElementAt(0).Offset;
                            oldExceptionHandlerMapping.tryStartOffset = tryStart.Offset;
                        }

                        // get offset of the instruction that ends the try block (always the last instruction of the basic block)
                        if (tempTryBlock.lastBasicBlockOfTryBlock) {
                            tryEnd.Offset = tempBB.operations.ElementAt(tempBB.operations.Count() - 1).Offset;
                            // exception handler object needs offset of the end of the instruction (not the beginning)
                            tryEnd.Offset += CfgBuilder.getSizeOfOperation(tempBB.operations.ElementAt(tempBB.operations.Count() - 1));
                            oldExceptionHandlerMapping.tryEndOffset = tryEnd.Offset;
                        }
                    }

                    foreach (HandlerBlock tempHandlerBlock in tempBB.handlerBlocks) {

                        // ignore handler blocks that do not belong to the current exception handler
                        if (tempHandlerBlock.exceptionHandler != exceptionHandler) {
                            continue;
                        }

                        // get offset ot the instruction that starts the handler block
                        if (tempHandlerBlock.firstBasicBlockOfHandlerBlock) {
                            handlerStart.Offset = tempBB.operations.ElementAt(0).Offset;
                            oldExceptionHandlerMapping.handlerStartOffset = handlerStart.Offset;
                        }

                        // get offset of the instruction that ends the handler block
                        if (tempHandlerBlock.lastBasicBlockOfHandlerBlock) {
                            handlerEnd.Offset = tempBB.operations.ElementAt(tempBB.operations.Count() - 1).Offset;
                            // exception handler object needs offset of the end of the instruction (not the beginning)
                            handlerEnd.Offset += CfgBuilder.getSizeOfOperation(tempBB.operations.ElementAt(tempBB.operations.Count() - 1));
                            oldExceptionHandlerMapping.handlerEndOffset = handlerEnd.Offset;
                        }
                    }
                }

                // copy the exception handler filter
                filterStart.Offset = exceptionHandler.FilterDecisionStartOffset;
                oldExceptionHandlerMapping.filterStartOffset = filterStart.Offset;

                // add new exception handler
                oldExceptionHandlerMapping.exceptionType = exceptionHandler.ExceptionType;
                oldExceptionHandlerMapping.handlerKind = exceptionHandler.HandlerKind;
                ilGenerator.AddExceptionHandlerInformation(exceptionHandler.HandlerKind, exceptionHandler.ExceptionType, tryStart, tryEnd, handlerStart, handlerEnd, filterStart);
            }

            // create the body
            List<ILocalDefinition> variableListCopy = new List<ILocalDefinition>(method.Body.LocalVariables);
            List<ITypeDefinition> privateHelperTypesListCopy = new List<ITypeDefinition>(method.Body.PrivateHelperTypes);
            var newBody = new ILGeneratorMethodBody(ilGenerator, method.Body.LocalsAreZeroed, 8, method, variableListCopy, privateHelperTypesListCopy); // TODO dynamic max stack size?
            method.Body = newBody;


            // step six: replace pointer to the old exception handler with the new exception handler

            // map all old exception handler to the new objects
            foreach (IOperationExceptionInformation exceptionHandler in method.Body.OperationExceptionInformation) {

                // search for the new exception handler object to get the mapping to the old one
                bool found = false;
                foreach (OldExceptionHandlerMapping oldExceptionHandlerMapping in oldExceptionHandlerMappings) {
                    if (oldExceptionHandlerMapping.exceptionType == exceptionHandler.ExceptionType
                        && oldExceptionHandlerMapping.handlerKind == exceptionHandler.HandlerKind
                        && oldExceptionHandlerMapping.filterStartOffset == exceptionHandler.FilterDecisionStartOffset
                        && oldExceptionHandlerMapping.tryStartOffset == exceptionHandler.TryStartOffset
                        && oldExceptionHandlerMapping.tryEndOffset == exceptionHandler.TryEndOffset
                        && oldExceptionHandlerMapping.handlerStartOffset == exceptionHandler.HandlerStartOffset
                        && oldExceptionHandlerMapping.handlerEndOffset == exceptionHandler.HandlerEndOffset) {
                        oldExceptionHandlerMapping.newExceptionHandler = exceptionHandler;
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    throw new ArgumentException("Not able to map old exception handler to new one.");
                }
            }

            // replace all old exception handler in the basic blocks with the new exception handler
            foreach (BasicBlock tempBB in methodCfg.basicBlocks) {

                // replace all exception handler in the try blocks
                foreach (TryBlock tryBlock in tempBB.tryBlocks) {
                    foreach (OldExceptionHandlerMapping oldExceptionHandlerMapping in oldExceptionHandlerMappings) {
                        if (tryBlock.exceptionHandler == oldExceptionHandlerMapping.oldExceptionHandler) {
                            tryBlock.exceptionHandler = oldExceptionHandlerMapping.newExceptionHandler;
                            break;
                        }
                    }
                }

                // replace all exception handler in the handler blocks
                foreach (HandlerBlock handlerBlock in tempBB.handlerBlocks) {
                    foreach (OldExceptionHandlerMapping oldExceptionHandlerMapping in oldExceptionHandlerMappings) {
                        if (handlerBlock.exceptionHandler == oldExceptionHandlerMapping.oldExceptionHandler) {
                            handlerBlock.exceptionHandler = oldExceptionHandlerMapping.newExceptionHandler;
                            break;
                        }
                    }
                }
            }

            
            // TODO
            // DEBUG
            /*
            // sanitize method name to store it as a file
            String invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            String invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            String fileName = System.Text.RegularExpressions.Regex.Replace(methodCfg.method.ToString(), invalidRegStr, "_");
            fileName = fileName.Length >= 230 ? fileName.Substring(0, 230) : fileName;

            // dump cfg created from the exit branches
            System.IO.StreamWriter dotFile = new System.IO.StreamWriter("e:\\" + "\\BBUnion_" + fileName + ".dot");

            // start .dot file graph
            dotFile.WriteLine("digraph G {");

            for (int bbuidx = 0; bbuidx < basicBlockUnions.Count(); bbuidx++) {
                BasicBlockUnion basicBlockUnion = basicBlockUnions.ElementAt(bbuidx);
                    // write all basic blocks to .dot file
                    for (int idx = 0; idx < basicBlockUnion.basicBlocks.Count(); idx++) {

                        BasicBlock currentBasicBlock = basicBlockUnion.basicBlocks.ElementAt(idx);

                        // write the current basic block to the file and all its instructions
                        dotFile.WriteLine("BB" + bbuidx.ToString() + "_" + idx.ToString() + " [shape=record]");
                        bool first = true;
                        for (int opIdx = 0; opIdx < currentBasicBlock.operations.Count(); opIdx++) {
                            var operation = currentBasicBlock.operations.ElementAt(opIdx);
                            if (first) {
                                dotFile.Write("BB" + bbuidx.ToString() + "_" + idx.ToString() + " [label=\"{");
                                first = false;
                            }
                            else {
                                dotFile.Write("|");
                            }

                            // insert try block beginnings
                            foreach (var tryBlock in currentBasicBlock.tryBlocks) {
                                if (tryBlock.firstBasicBlockOfTryBlock && opIdx == 0) {
                                    dotFile.Write("TRY START (" + tryBlock.exceptionHandler.ExceptionType.ToString() + ")|");
                                }
                            }

                            // insert catch block beginnings
                            foreach (var handlerBlock in currentBasicBlock.handlerBlocks) {
                                if (handlerBlock.firstBasicBlockOfHandlerBlock && opIdx == 0) {
                                    if (handlerBlock.typeOfHandler == HandlerKind.Catch) {
                                        dotFile.Write("CATCH START (" + handlerBlock.exceptionHandler.ExceptionType.ToString() + ")|");
                                    }
                                    else if (handlerBlock.typeOfHandler == HandlerKind.Finally) {
                                        dotFile.Write("FINALLY START (" + handlerBlock.exceptionHandler.ExceptionType.ToString() + ")|");
                                    }
                                    else {
                                        throw new ArgumentException("Do not know how to handle handler.");
                                    }
                                }
                            }

                            // check if instruction has an argument
                            if (operation.Value != null) {
                                dotFile.Write(operation.OperationCode.ToString() + " " + operation.Value.ToString());
                            }
                            else {
                                dotFile.Write(operation.OperationCode.ToString());
                            }

                            // insert try block endings
                            foreach (var tryBlock in currentBasicBlock.tryBlocks) {
                                if (tryBlock.lastBasicBlockOfTryBlock && (currentBasicBlock.operations.Count() - 1) == opIdx) {
                                    dotFile.Write("|TRY END (" + tryBlock.exceptionHandler.ExceptionType.ToString() + ")");
                                }
                            }

                            // insert catch block endings
                            foreach (var handlerBlock in currentBasicBlock.handlerBlocks) {
                                if (handlerBlock.lastBasicBlockOfHandlerBlock && (currentBasicBlock.operations.Count() - 1) == opIdx) {
                                    if (handlerBlock.typeOfHandler == HandlerKind.Catch) {
                                        dotFile.Write("|CATCH END (" + handlerBlock.exceptionHandler.ExceptionType.ToString() + ")");
                                    }
                                    else if (handlerBlock.typeOfHandler == HandlerKind.Finally) {
                                        dotFile.Write("|FINALLY END (" + handlerBlock.exceptionHandler.ExceptionType.ToString() + ")");
                                    }
                                    else {
                                        throw new ArgumentException("Do not know how to handle handler.");
                                    }
                                }
                            }
                        }
                        dotFile.WriteLine("}\"]");


                        if ((idx + 1) < basicBlockUnion.basicBlocks.Count()) {
                            dotFile.WriteLine("BB" + bbuidx.ToString() + "_" + idx.ToString() + " -> BB" + bbuidx.ToString() + "_" + (idx + 1).ToString() + "[ color=\"blue\" ]");
                        }

                    }
                }


            // finish graph
            dotFile.WriteLine("}");

            dotFile.Close();
            */
        }
    }
}
