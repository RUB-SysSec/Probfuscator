using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Cci;
using Microsoft.Cci.MutableCodeModel;
using TransformationsMetadata;

namespace CfgElements {

    // interface that have to be implemented by all branches in the CFG
    public interface IBranchTarget {
        BasicBlock sourceBasicBlock { get; set; }
    }


    // class represents a "virtual" branch (i.e. a basic block is splitten into two basic blocks
    // because a branch has the second half as a target)
    public class NoBranchTarget : IBranchTarget {

        public BasicBlock sourceBasicBlock { get; set; }

        // the target basic block that comes directly after the current one
        // (used if a branch divides a basic block into two)
        public BasicBlock takenTarget;
    }


    // class represents a unconditional branch (i.e. be)
    public class UnconditionalBranchTarget : IBranchTarget {

        public BasicBlock sourceBasicBlock { get; set; }

        // the target basic block of the unconditional branch (which is always taken)
        public BasicBlock takenTarget;
    }


    // class represents a conditional branch (i.e. beq)
    public class ConditionalBranchTarget : IBranchTarget {

        public BasicBlock sourceBasicBlock { get; set; }

        // the basic block which is reached when the branch is not taken
        // (the instruction directly after the conditional branch instruction)
        public BasicBlock notTakenTarget;

        // the basic block which is reached when the branch is taken
        public BasicBlock takenTarget;
    }


    // class represents a switch branch (this can have more than one taken target and a not taken target)
    public class SwitchBranchTarget : IBranchTarget {

        public BasicBlock sourceBasicBlock { get; set; }

        // the basic block which is reached when the branch is not taken
        // (in general the default case of the switch statement)
        public BasicBlock notTakenTarget;

        // the basic blocks which can be reached by the switch instruction when it is taken
        // NOTE: the order is important in this case, it is the same order in which the arguments
        // of the instruction were read by the analyzer
        public List<BasicBlock> takenTarget = new List<BasicBlock>();
    }


    // class represents an exit branch (i.e. after a ret instruction)
    public class ExitBranchTarget : IBranchTarget {

        public BasicBlock sourceBasicBlock { get; set; }
    }


    // class represents an exit branch that is done via the "throw" instruction
    // (not necessarily an exit of the function, because the exception can be catched inside of it)
    public class ThrowBranchTarget : IBranchTarget {

        public BasicBlock sourceBasicBlock { get; set; }
    }


    // class represents a "virtual" branch that is used when a try block starts
    public class TryBlockTarget : IBranchTarget {

        public BasicBlock sourceBasicBlock { get; set; }

        // the target basic block that comes directly after the current one
        // (used if a branch divides a basic block into two)
        public BasicBlock takenTarget;
    }


    // class represents a branch that exits an exception handler (caused by a "leave" instruction)
    public class ExceptionBranchTarget : IBranchTarget {

        public BasicBlock sourceBasicBlock { get; set; }

        // the basic block which is reached when the exception is triggered
        // (the instruction follows directly after the try block or catch block in case of multiple catches)
        public BasicBlock exceptionTarget;

        // the basic block which is jumped to when protected region is exited
        public BasicBlock exitTarget;
    }


    // class represtens a CFG of a specific class
    // (just a wrapper class that contains all CFGs of each method of the class)
    public class ClassCfg {
        public List<MethodCfg> methodCfgs = new List<MethodCfg>();
        public NamespaceTypeDefinition classObj;
    }


    // class represents a CFG of a specific method
    public class MethodCfg {

        // an internal class that represents an operation
        // (the attributes of the CCI operations are not writeable)
        internal class InternalOperation : IOperation {
            public OperationCode OperationCode { get; set; }
            public uint Offset { get; set; }
            public ILocation Location { get; set; }
            public object/*?*/ Value { get; set; }
        }


        public BasicBlock startBasicBlock;
        public List<BasicBlock> basicBlocks = new List<BasicBlock>();
        public MethodDefinition method;


        public MethodCfg() {
        }


        // this constructor copies the given source method cfg and all its elements
        // NOTE: it ignores the transformation metadata
        public MethodCfg(MethodCfg sourceMethodCfg) {

            // copy link to the method
            this.method = sourceMethodCfg.method;

            // generate a list of already processed basic blocks
            List<BasicBlock> processedBasicBlocks = new List<BasicBlock>();

            // set start basic block of the CFG as already processed
            processedBasicBlocks.Add(sourceMethodCfg.startBasicBlock);

            // create copy of start basic block
            BasicBlock copiedStartBasicBlock = new BasicBlock();
            this.startBasicBlock = copiedStartBasicBlock;
            this.basicBlocks.Add(copiedStartBasicBlock);

            // copy simple values
            copiedStartBasicBlock.id = sourceMethodCfg.startBasicBlock.id;
            copiedStartBasicBlock.semanticId = sourceMethodCfg.startBasicBlock.semanticId;
            copiedStartBasicBlock.startIdx = sourceMethodCfg.startBasicBlock.startIdx;
            copiedStartBasicBlock.endIdx = sourceMethodCfg.startBasicBlock.endIdx;

            // copy all operations of the basic block
            foreach (IOperation operation in sourceMethodCfg.startBasicBlock.operations) {
                InternalOperation copiedOperation = new InternalOperation();
                copiedOperation.OperationCode = operation.OperationCode;
                copiedOperation.Value = operation.Value;
                copiedOperation.Offset = operation.Offset;
                copiedOperation.Location = operation.Location;
                copiedStartBasicBlock.operations.Add(copiedOperation);
            }


            // process rest of the cfg and copy the exit branch
            if ((sourceMethodCfg.startBasicBlock.exitBranch as NoBranchTarget) != null) {
                NoBranchTarget exitBranch = (sourceMethodCfg.startBasicBlock.exitBranch as NoBranchTarget);

                // create a copied exit branch
                NoBranchTarget copiedExitBranch = new NoBranchTarget();
                copiedExitBranch.sourceBasicBlock = copiedStartBasicBlock;
                copiedStartBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceMethodCfg.startBasicBlock.exitBranch as UnconditionalBranchTarget != null) {
                UnconditionalBranchTarget exitBranch = (sourceMethodCfg.startBasicBlock.exitBranch as UnconditionalBranchTarget);

                // create a copied exit branch
                UnconditionalBranchTarget copiedExitBranch = new UnconditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = copiedStartBasicBlock;
                copiedStartBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceMethodCfg.startBasicBlock.exitBranch as ConditionalBranchTarget != null) {
                ConditionalBranchTarget exitBranch = (sourceMethodCfg.startBasicBlock.exitBranch as ConditionalBranchTarget);

                // create a copied exit branch
                ConditionalBranchTarget copiedExitBranch = new ConditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = copiedStartBasicBlock;
                copiedStartBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch, true);

                // process branch and next basic block
                nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.notTakenTarget, ref nextCopiedBasicBlock, copiedExitBranch, false);

            }

            else if (sourceMethodCfg.startBasicBlock.exitBranch as SwitchBranchTarget != null) {
                SwitchBranchTarget exitBranch = (sourceMethodCfg.startBasicBlock.exitBranch as SwitchBranchTarget);

                // create a copied exit branch
                SwitchBranchTarget copiedExitBranch = new SwitchBranchTarget();
                copiedExitBranch.sourceBasicBlock = copiedStartBasicBlock;
                copiedStartBasicBlock.exitBranch = copiedExitBranch;

                // first process not taken branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.notTakenTarget, ref nextCopiedBasicBlock, copiedExitBranch, -1);

                // process all taken branches and next basic blocks
                for (int idx = 0; idx < exitBranch.takenTarget.Count(); idx++) {
                    nextCopiedBasicBlock = null;
                    this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget.ElementAt(idx), ref nextCopiedBasicBlock, copiedExitBranch, idx);
                }

            }

            else if (sourceMethodCfg.startBasicBlock.exitBranch as ExitBranchTarget != null) {

                // create a copied exit branch
                ExitBranchTarget copiedExitBranch = new ExitBranchTarget();
                copiedExitBranch.sourceBasicBlock = copiedStartBasicBlock;
                copiedStartBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (sourceMethodCfg.startBasicBlock.exitBranch as ThrowBranchTarget != null) {

                // create a copied exit branch
                ThrowBranchTarget copiedExitBranch = new ThrowBranchTarget();
                copiedExitBranch.sourceBasicBlock = copiedStartBasicBlock;
                copiedStartBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (sourceMethodCfg.startBasicBlock.exitBranch as TryBlockTarget != null) {
                TryBlockTarget exitBranch = (sourceMethodCfg.startBasicBlock.exitBranch as TryBlockTarget);

                // create a copied exit branch
                TryBlockTarget copiedExitBranch = new TryBlockTarget();
                copiedExitBranch.sourceBasicBlock = copiedStartBasicBlock;
                copiedStartBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceMethodCfg.startBasicBlock.exitBranch as ExceptionBranchTarget != null) {
                ExceptionBranchTarget exitBranch = (sourceMethodCfg.startBasicBlock.exitBranch as ExceptionBranchTarget);

                // create a copied exit branch
                ExceptionBranchTarget copiedExitBranch = new ExceptionBranchTarget();
                copiedExitBranch.sourceBasicBlock = copiedStartBasicBlock;
                copiedStartBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.exceptionTarget, ref nextCopiedBasicBlock, copiedExitBranch, true);

                // process branch and next basic block
                nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.exitTarget, ref nextCopiedBasicBlock, copiedExitBranch, false);

            }

            else {
                throw new ArgumentException("Do not know how to handle exit branch.");
            }


            // copy all try blocks
            foreach (TryBlock sourceTryBlock in sourceMethodCfg.startBasicBlock.tryBlocks) {

                TryBlock copiedTryBlock = new TryBlock();
                copiedTryBlock.exceptionHandler = sourceTryBlock.exceptionHandler;
                copiedTryBlock.firstBasicBlockOfTryBlock = sourceTryBlock.firstBasicBlockOfTryBlock;
                copiedTryBlock.lastBasicBlockOfTryBlock = sourceTryBlock.lastBasicBlockOfTryBlock;

                copiedStartBasicBlock.tryBlocks.Add(copiedTryBlock);
            }


            // copy all handler blocks
            foreach (HandlerBlock sourceHandlerBlock in sourceMethodCfg.startBasicBlock.handlerBlocks) {

                HandlerBlock copiedHandlerBlock = new HandlerBlock();
                copiedHandlerBlock.exceptionHandler = sourceHandlerBlock.exceptionHandler;
                copiedHandlerBlock.typeOfHandler = sourceHandlerBlock.typeOfHandler;
                copiedHandlerBlock.firstBasicBlockOfHandlerBlock = sourceHandlerBlock.firstBasicBlockOfHandlerBlock;
                copiedHandlerBlock.lastBasicBlockOfHandlerBlock = sourceHandlerBlock.lastBasicBlockOfHandlerBlock;

                copiedStartBasicBlock.handlerBlocks.Add(copiedHandlerBlock);
            }

        }


        // copies recursively the next basic block of the method CFG (with a no branch as entry branch)
        private void copyMethodCfgRecursively(List<BasicBlock> processedBasicBlocks, BasicBlock sourceBasicBlock, ref BasicBlock targetBasicBlock, NoBranchTarget copiedEntryBranch) {

            // check if source basic block was already processed
            if(processedBasicBlocks.Contains(sourceBasicBlock)) {

                bool found = false;
                foreach (BasicBlock searchBasicBlock in this.basicBlocks) {
                    if (searchBasicBlock.id == sourceBasicBlock.id) {

                        // set entry branch target
                        copiedEntryBranch.takenTarget = searchBasicBlock;
                        searchBasicBlock.entryBranches.Add(copiedEntryBranch);

                        found = true;
                        break;
                    }
                }
                if (!found) {
                    throw new ArgumentException("Copied basic block was not found.");
                }

                return;
            }

            // add source basic block to the list of already processed basic blocks
            processedBasicBlocks.Add(sourceBasicBlock);

            // create copy of the source basic block
            targetBasicBlock = new BasicBlock();
            this.basicBlocks.Add(targetBasicBlock);

            // set entry branch target
            copiedEntryBranch.takenTarget = targetBasicBlock;
            targetBasicBlock.entryBranches.Add(copiedEntryBranch);

            // copy simple values
            targetBasicBlock.id = sourceBasicBlock.id;
            targetBasicBlock.semanticId = sourceBasicBlock.semanticId;
            targetBasicBlock.startIdx = sourceBasicBlock.startIdx;
            targetBasicBlock.endIdx = sourceBasicBlock.endIdx;

            // copy all operations of the basic block
            foreach (IOperation operation in sourceBasicBlock.operations) {
                InternalOperation copiedOperation = new InternalOperation();
                copiedOperation.OperationCode = operation.OperationCode;
                copiedOperation.Value = operation.Value;
                copiedOperation.Offset = operation.Offset;
                copiedOperation.Location = operation.Location;
                targetBasicBlock.operations.Add(copiedOperation);
            }


            // process rest of the cfg and copy the exit branch
            if ((sourceBasicBlock.exitBranch as NoBranchTarget) != null) {
                NoBranchTarget exitBranch = (sourceBasicBlock.exitBranch as NoBranchTarget);

                // create a copied exit branch
                NoBranchTarget copiedExitBranch = new NoBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as UnconditionalBranchTarget != null) {
                UnconditionalBranchTarget exitBranch = (sourceBasicBlock.exitBranch as UnconditionalBranchTarget);

                // create a copied exit branch
                UnconditionalBranchTarget copiedExitBranch = new UnconditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as ConditionalBranchTarget != null) {
                ConditionalBranchTarget exitBranch = (sourceBasicBlock.exitBranch as ConditionalBranchTarget);

                // create a copied exit branch
                ConditionalBranchTarget copiedExitBranch = new ConditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch, true);

                // process branch and next basic block
                nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.notTakenTarget, ref nextCopiedBasicBlock, copiedExitBranch, false);

            }

            else if (sourceBasicBlock.exitBranch as SwitchBranchTarget != null) {
                SwitchBranchTarget exitBranch = (sourceBasicBlock.exitBranch as SwitchBranchTarget);

                // create a copied exit branch
                SwitchBranchTarget copiedExitBranch = new SwitchBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // first process not taken branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.notTakenTarget, ref nextCopiedBasicBlock, copiedExitBranch, -1);

                // process all taken branches and next basic blocks
                for (int idx = 0; idx < exitBranch.takenTarget.Count(); idx++) {
                    nextCopiedBasicBlock = null;
                    this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget.ElementAt(idx), ref nextCopiedBasicBlock, copiedExitBranch, idx);
                }

            }

            else if (sourceBasicBlock.exitBranch as ExitBranchTarget != null) {

                // create a copied exit branch
                ExitBranchTarget copiedExitBranch = new ExitBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (sourceBasicBlock.exitBranch as ThrowBranchTarget != null) {

                // create a copied exit branch
                ThrowBranchTarget copiedExitBranch = new ThrowBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (sourceBasicBlock.exitBranch as TryBlockTarget != null) {
                TryBlockTarget exitBranch = (sourceBasicBlock.exitBranch as TryBlockTarget);

                // create a copied exit branch
                TryBlockTarget copiedExitBranch = new TryBlockTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as ExceptionBranchTarget != null) {
                ExceptionBranchTarget exitBranch = (sourceBasicBlock.exitBranch as ExceptionBranchTarget);

                // create a copied exit branch
                ExceptionBranchTarget copiedExitBranch = new ExceptionBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.exceptionTarget, ref nextCopiedBasicBlock, copiedExitBranch, true);

                // process branch and next basic block
                nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.exitTarget, ref nextCopiedBasicBlock, copiedExitBranch, false);

            }

            else {
                throw new ArgumentException("Do not know how to handle exit branch.");
            }


            // copy all try blocks
            foreach (TryBlock sourceTryBlock in sourceBasicBlock.tryBlocks) {

                TryBlock copiedTryBlock = new TryBlock();
                copiedTryBlock.exceptionHandler = sourceTryBlock.exceptionHandler;
                copiedTryBlock.firstBasicBlockOfTryBlock = sourceTryBlock.firstBasicBlockOfTryBlock;
                copiedTryBlock.lastBasicBlockOfTryBlock = sourceTryBlock.lastBasicBlockOfTryBlock;

                targetBasicBlock.tryBlocks.Add(copiedTryBlock);
            }


            // copy all handler blocks
            foreach (HandlerBlock sourceHandlerBlock in sourceBasicBlock.handlerBlocks) {

                HandlerBlock copiedHandlerBlock = new HandlerBlock();
                copiedHandlerBlock.exceptionHandler = sourceHandlerBlock.exceptionHandler;
                copiedHandlerBlock.typeOfHandler = sourceHandlerBlock.typeOfHandler;
                copiedHandlerBlock.firstBasicBlockOfHandlerBlock = sourceHandlerBlock.firstBasicBlockOfHandlerBlock;
                copiedHandlerBlock.lastBasicBlockOfHandlerBlock = sourceHandlerBlock.lastBasicBlockOfHandlerBlock;

                targetBasicBlock.handlerBlocks.Add(copiedHandlerBlock);
            }

        }


        // copies recursively the next basic block of the method CFG (with an unconditional branch as entry branch)
        private void copyMethodCfgRecursively(List<BasicBlock> processedBasicBlocks, BasicBlock sourceBasicBlock, ref BasicBlock targetBasicBlock, UnconditionalBranchTarget copiedEntryBranch) {

            // check if source basic block was already processed
            if (processedBasicBlocks.Contains(sourceBasicBlock)) {

                bool found = false;
                foreach (BasicBlock searchBasicBlock in this.basicBlocks) {
                    if (searchBasicBlock.id == sourceBasicBlock.id) {

                        // set entry branch target
                        copiedEntryBranch.takenTarget = searchBasicBlock;
                        searchBasicBlock.entryBranches.Add(copiedEntryBranch);

                        found = true;
                        break;
                    }
                }
                if (!found) {
                    throw new ArgumentException("Copied basic block was not found.");
                }

                return;
            }

            // add source basic block to the list of already processed basic blocks
            processedBasicBlocks.Add(sourceBasicBlock);

            // create copy of the source basic block
            targetBasicBlock = new BasicBlock();
            this.basicBlocks.Add(targetBasicBlock);

            // set entry branch target
            copiedEntryBranch.takenTarget = targetBasicBlock;
            targetBasicBlock.entryBranches.Add(copiedEntryBranch);

            // copy simple values
            targetBasicBlock.id = sourceBasicBlock.id;
            targetBasicBlock.semanticId = sourceBasicBlock.semanticId;
            targetBasicBlock.startIdx = sourceBasicBlock.startIdx;
            targetBasicBlock.endIdx = sourceBasicBlock.endIdx;

            // copy all operations of the basic block
            foreach (IOperation operation in sourceBasicBlock.operations) {
                InternalOperation copiedOperation = new InternalOperation();
                copiedOperation.OperationCode = operation.OperationCode;
                copiedOperation.Value = operation.Value;
                copiedOperation.Offset = operation.Offset;
                copiedOperation.Location = operation.Location;
                targetBasicBlock.operations.Add(copiedOperation);
            }


            // process rest of the cfg and copy the exit branch
            if ((sourceBasicBlock.exitBranch as NoBranchTarget) != null) {
                NoBranchTarget exitBranch = (sourceBasicBlock.exitBranch as NoBranchTarget);

                // create a copied exit branch
                NoBranchTarget copiedExitBranch = new NoBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as UnconditionalBranchTarget != null) {
                UnconditionalBranchTarget exitBranch = (sourceBasicBlock.exitBranch as UnconditionalBranchTarget);

                // create a copied exit branch
                UnconditionalBranchTarget copiedExitBranch = new UnconditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as ConditionalBranchTarget != null) {
                ConditionalBranchTarget exitBranch = (sourceBasicBlock.exitBranch as ConditionalBranchTarget);

                // create a copied exit branch
                ConditionalBranchTarget copiedExitBranch = new ConditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch, true);

                // process branch and next basic block
                nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.notTakenTarget, ref nextCopiedBasicBlock, copiedExitBranch, false);

            }

            else if (sourceBasicBlock.exitBranch as SwitchBranchTarget != null) {
                SwitchBranchTarget exitBranch = (sourceBasicBlock.exitBranch as SwitchBranchTarget);

                // create a copied exit branch
                SwitchBranchTarget copiedExitBranch = new SwitchBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // first process not taken branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.notTakenTarget, ref nextCopiedBasicBlock, copiedExitBranch, -1);

                // process all taken branches and next basic blocks
                for (int idx = 0; idx < exitBranch.takenTarget.Count(); idx++) {
                    nextCopiedBasicBlock = null;
                    this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget.ElementAt(idx), ref nextCopiedBasicBlock, copiedExitBranch, idx);
                }

            }

            else if (sourceBasicBlock.exitBranch as ExitBranchTarget != null) {

                // create a copied exit branch
                ExitBranchTarget copiedExitBranch = new ExitBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (sourceBasicBlock.exitBranch as ThrowBranchTarget != null) {

                // create a copied exit branch
                ThrowBranchTarget copiedExitBranch = new ThrowBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (sourceBasicBlock.exitBranch as TryBlockTarget != null) {
                TryBlockTarget exitBranch = (sourceBasicBlock.exitBranch as TryBlockTarget);

                // create a copied exit branch
                TryBlockTarget copiedExitBranch = new TryBlockTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as ExceptionBranchTarget != null) {
                ExceptionBranchTarget exitBranch = (sourceBasicBlock.exitBranch as ExceptionBranchTarget);

                // create a copied exit branch
                ExceptionBranchTarget copiedExitBranch = new ExceptionBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.exceptionTarget, ref nextCopiedBasicBlock, copiedExitBranch, true);

                // process branch and next basic block
                nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.exitTarget, ref nextCopiedBasicBlock, copiedExitBranch, false);

            }

            else {
                throw new ArgumentException("Do not know how to handle exit branch.");
            }


            // copy all try blocks
            foreach (TryBlock sourceTryBlock in sourceBasicBlock.tryBlocks) {

                TryBlock copiedTryBlock = new TryBlock();
                copiedTryBlock.exceptionHandler = sourceTryBlock.exceptionHandler;
                copiedTryBlock.firstBasicBlockOfTryBlock = sourceTryBlock.firstBasicBlockOfTryBlock;
                copiedTryBlock.lastBasicBlockOfTryBlock = sourceTryBlock.lastBasicBlockOfTryBlock;

                targetBasicBlock.tryBlocks.Add(copiedTryBlock);
            }


            // copy all handler blocks
            foreach (HandlerBlock sourceHandlerBlock in sourceBasicBlock.handlerBlocks) {

                HandlerBlock copiedHandlerBlock = new HandlerBlock();
                copiedHandlerBlock.exceptionHandler = sourceHandlerBlock.exceptionHandler;
                copiedHandlerBlock.typeOfHandler = sourceHandlerBlock.typeOfHandler;
                copiedHandlerBlock.firstBasicBlockOfHandlerBlock = sourceHandlerBlock.firstBasicBlockOfHandlerBlock;
                copiedHandlerBlock.lastBasicBlockOfHandlerBlock = sourceHandlerBlock.lastBasicBlockOfHandlerBlock;

                targetBasicBlock.handlerBlocks.Add(copiedHandlerBlock);
            }

        }


        // copies recursively the next basic block of the method CFG (with a conditional branch as entry branch)
        private void copyMethodCfgRecursively(List<BasicBlock> processedBasicBlocks, BasicBlock sourceBasicBlock, ref BasicBlock targetBasicBlock, ConditionalBranchTarget copiedEntryBranch, bool takenEntryBranch) {

            // check if source basic block was already processed
            if (processedBasicBlocks.Contains(sourceBasicBlock)) {

                bool found = false;
                foreach (BasicBlock searchBasicBlock in this.basicBlocks) {
                    if (searchBasicBlock.id == sourceBasicBlock.id) {

                        // set entry branch target
                        if (takenEntryBranch) {                            
                            copiedEntryBranch.takenTarget = searchBasicBlock;
                            
                        }
                        else {
                            copiedEntryBranch.notTakenTarget = searchBasicBlock;
                        }

                        searchBasicBlock.entryBranches.Add(copiedEntryBranch);
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    throw new ArgumentException("Copied basic block was not found.");
                }

                return;
            }

            // add source basic block to the list of already processed basic blocks
            processedBasicBlocks.Add(sourceBasicBlock);

            // create copy of the source basic block
            targetBasicBlock = new BasicBlock();
            this.basicBlocks.Add(targetBasicBlock);

            // set entry branch target
            if (takenEntryBranch) {
                copiedEntryBranch.takenTarget = targetBasicBlock;
            }
            else {
                copiedEntryBranch.notTakenTarget = targetBasicBlock;
            }
            targetBasicBlock.entryBranches.Add(copiedEntryBranch);

            // copy simple values
            targetBasicBlock.id = sourceBasicBlock.id;
            targetBasicBlock.semanticId = sourceBasicBlock.semanticId;
            targetBasicBlock.startIdx = sourceBasicBlock.startIdx;
            targetBasicBlock.endIdx = sourceBasicBlock.endIdx;

            // copy all operations of the basic block
            foreach (IOperation operation in sourceBasicBlock.operations) {
                InternalOperation copiedOperation = new InternalOperation();
                copiedOperation.OperationCode = operation.OperationCode;
                copiedOperation.Value = operation.Value;
                copiedOperation.Offset = operation.Offset;
                copiedOperation.Location = operation.Location;
                targetBasicBlock.operations.Add(copiedOperation);
            }


            // process rest of the cfg and copy the exit branch
            if ((sourceBasicBlock.exitBranch as NoBranchTarget) != null) {
                NoBranchTarget exitBranch = (sourceBasicBlock.exitBranch as NoBranchTarget);

                // create a copied exit branch
                NoBranchTarget copiedExitBranch = new NoBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as UnconditionalBranchTarget != null) {
                UnconditionalBranchTarget exitBranch = (sourceBasicBlock.exitBranch as UnconditionalBranchTarget);

                // create a copied exit branch
                UnconditionalBranchTarget copiedExitBranch = new UnconditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as ConditionalBranchTarget != null) {
                ConditionalBranchTarget exitBranch = (sourceBasicBlock.exitBranch as ConditionalBranchTarget);

                // create a copied exit branch
                ConditionalBranchTarget copiedExitBranch = new ConditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch, true);

                // process branch and next basic block
                nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.notTakenTarget, ref nextCopiedBasicBlock, copiedExitBranch, false);

            }

            else if (sourceBasicBlock.exitBranch as SwitchBranchTarget != null) {
                SwitchBranchTarget exitBranch = (sourceBasicBlock.exitBranch as SwitchBranchTarget);

                // create a copied exit branch
                SwitchBranchTarget copiedExitBranch = new SwitchBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // first process not taken branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.notTakenTarget, ref nextCopiedBasicBlock, copiedExitBranch, -1);

                // process all taken branches and next basic blocks
                for (int idx = 0; idx < exitBranch.takenTarget.Count(); idx++) {
                    nextCopiedBasicBlock = null;
                    this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget.ElementAt(idx), ref nextCopiedBasicBlock, copiedExitBranch, idx);
                }

            }

            else if (sourceBasicBlock.exitBranch as ExitBranchTarget != null) {

                // create a copied exit branch
                ExitBranchTarget copiedExitBranch = new ExitBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (sourceBasicBlock.exitBranch as ThrowBranchTarget != null) {

                // create a copied exit branch
                ThrowBranchTarget copiedExitBranch = new ThrowBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (sourceBasicBlock.exitBranch as TryBlockTarget != null) {
                TryBlockTarget exitBranch = (sourceBasicBlock.exitBranch as TryBlockTarget);

                // create a copied exit branch
                TryBlockTarget copiedExitBranch = new TryBlockTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as ExceptionBranchTarget != null) {
                ExceptionBranchTarget exitBranch = (sourceBasicBlock.exitBranch as ExceptionBranchTarget);

                // create a copied exit branch
                ExceptionBranchTarget copiedExitBranch = new ExceptionBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.exceptionTarget, ref nextCopiedBasicBlock, copiedExitBranch, true);

                // process branch and next basic block
                nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.exitTarget, ref nextCopiedBasicBlock, copiedExitBranch, false);

            }

            else {
                throw new ArgumentException("Do not know how to handle exit branch.");
            }


            // copy all try blocks
            foreach (TryBlock sourceTryBlock in sourceBasicBlock.tryBlocks) {

                TryBlock copiedTryBlock = new TryBlock();
                copiedTryBlock.exceptionHandler = sourceTryBlock.exceptionHandler;
                copiedTryBlock.firstBasicBlockOfTryBlock = sourceTryBlock.firstBasicBlockOfTryBlock;
                copiedTryBlock.lastBasicBlockOfTryBlock = sourceTryBlock.lastBasicBlockOfTryBlock;

                targetBasicBlock.tryBlocks.Add(copiedTryBlock);
            }


            // copy all handler blocks
            foreach (HandlerBlock sourceHandlerBlock in sourceBasicBlock.handlerBlocks) {

                HandlerBlock copiedHandlerBlock = new HandlerBlock();
                copiedHandlerBlock.exceptionHandler = sourceHandlerBlock.exceptionHandler;
                copiedHandlerBlock.typeOfHandler = sourceHandlerBlock.typeOfHandler;
                copiedHandlerBlock.firstBasicBlockOfHandlerBlock = sourceHandlerBlock.firstBasicBlockOfHandlerBlock;
                copiedHandlerBlock.lastBasicBlockOfHandlerBlock = sourceHandlerBlock.lastBasicBlockOfHandlerBlock;

                targetBasicBlock.handlerBlocks.Add(copiedHandlerBlock);
            }

        }


        // copies recursively the next basic block of the method CFG (with a switch branch as entry branch)
        private void copyMethodCfgRecursively(List<BasicBlock> processedBasicBlocks, BasicBlock sourceBasicBlock, ref BasicBlock targetBasicBlock, SwitchBranchTarget copiedEntryBranch, int takenEntryBranchIdx) {

            // check if source basic block was already processed
            if (processedBasicBlocks.Contains(sourceBasicBlock)) {

                bool found = false;
                foreach (BasicBlock searchBasicBlock in this.basicBlocks) {
                    if (searchBasicBlock.id == sourceBasicBlock.id) {

                        // set entry branch target
                        if (takenEntryBranchIdx == -1) {
                            copiedEntryBranch.notTakenTarget = searchBasicBlock;

                        }
                        else {

                            // fill switch taken branch list with empty elements when index is out of range
                            while (copiedEntryBranch.takenTarget.Count() <= takenEntryBranchIdx) {
                                copiedEntryBranch.takenTarget.Add(null);
                            }

                            copiedEntryBranch.takenTarget[takenEntryBranchIdx] = searchBasicBlock;
                        }

                        searchBasicBlock.entryBranches.Add(copiedEntryBranch);
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    throw new ArgumentException("Copied basic block was not found.");
                }

                return;
            }

            // add source basic block to the list of already processed basic blocks
            processedBasicBlocks.Add(sourceBasicBlock);

            // create copy of the source basic block
            targetBasicBlock = new BasicBlock();
            this.basicBlocks.Add(targetBasicBlock);

            // set entry branch target
            if (takenEntryBranchIdx == -1) {
                copiedEntryBranch.notTakenTarget = targetBasicBlock;

            }
            else {

                // fill switch taken branch list with empty elements when index is out of range
                while (copiedEntryBranch.takenTarget.Count() <= takenEntryBranchIdx) {
                    copiedEntryBranch.takenTarget.Add(null);
                }

                copiedEntryBranch.takenTarget[takenEntryBranchIdx] = targetBasicBlock;
            }
            targetBasicBlock.entryBranches.Add(copiedEntryBranch);

            // copy simple values
            targetBasicBlock.id = sourceBasicBlock.id;
            targetBasicBlock.semanticId = sourceBasicBlock.semanticId;
            targetBasicBlock.startIdx = sourceBasicBlock.startIdx;
            targetBasicBlock.endIdx = sourceBasicBlock.endIdx;

            // copy all operations of the basic block
            foreach (IOperation operation in sourceBasicBlock.operations) {
                InternalOperation copiedOperation = new InternalOperation();
                copiedOperation.OperationCode = operation.OperationCode;
                copiedOperation.Value = operation.Value;
                copiedOperation.Offset = operation.Offset;
                copiedOperation.Location = operation.Location;
                targetBasicBlock.operations.Add(copiedOperation);
            }


            // process rest of the cfg and copy the exit branch
            if ((sourceBasicBlock.exitBranch as NoBranchTarget) != null) {
                NoBranchTarget exitBranch = (sourceBasicBlock.exitBranch as NoBranchTarget);

                // create a copied exit branch
                NoBranchTarget copiedExitBranch = new NoBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as UnconditionalBranchTarget != null) {
                UnconditionalBranchTarget exitBranch = (sourceBasicBlock.exitBranch as UnconditionalBranchTarget);

                // create a copied exit branch
                UnconditionalBranchTarget copiedExitBranch = new UnconditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as ConditionalBranchTarget != null) {
                ConditionalBranchTarget exitBranch = (sourceBasicBlock.exitBranch as ConditionalBranchTarget);

                // create a copied exit branch
                ConditionalBranchTarget copiedExitBranch = new ConditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch, true);

                // process branch and next basic block
                nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.notTakenTarget, ref nextCopiedBasicBlock, copiedExitBranch, false);

            }

            else if (sourceBasicBlock.exitBranch as SwitchBranchTarget != null) {
                SwitchBranchTarget exitBranch = (sourceBasicBlock.exitBranch as SwitchBranchTarget);

                // create a copied exit branch
                SwitchBranchTarget copiedExitBranch = new SwitchBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // first process not taken branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.notTakenTarget, ref nextCopiedBasicBlock, copiedExitBranch, -1);

                // process all taken branches and next basic blocks
                for (int idx = 0; idx < exitBranch.takenTarget.Count(); idx++) {
                    nextCopiedBasicBlock = null;
                    this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget.ElementAt(idx), ref nextCopiedBasicBlock, copiedExitBranch, idx);
                }

            }

            else if (sourceBasicBlock.exitBranch as ExitBranchTarget != null) {

                // create a copied exit branch
                ExitBranchTarget copiedExitBranch = new ExitBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (sourceBasicBlock.exitBranch as ThrowBranchTarget != null) {

                // create a copied exit branch
                ThrowBranchTarget copiedExitBranch = new ThrowBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (sourceBasicBlock.exitBranch as TryBlockTarget != null) {
                TryBlockTarget exitBranch = (sourceBasicBlock.exitBranch as TryBlockTarget);

                // create a copied exit branch
                TryBlockTarget copiedExitBranch = new TryBlockTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as ExceptionBranchTarget != null) {
                ExceptionBranchTarget exitBranch = (sourceBasicBlock.exitBranch as ExceptionBranchTarget);

                // create a copied exit branch
                ExceptionBranchTarget copiedExitBranch = new ExceptionBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.exceptionTarget, ref nextCopiedBasicBlock, copiedExitBranch, true);

                // process branch and next basic block
                nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.exitTarget, ref nextCopiedBasicBlock, copiedExitBranch, false);

            }

            else {
                throw new ArgumentException("Do not know how to handle exit branch.");
            }


            // copy all try blocks
            foreach (TryBlock sourceTryBlock in sourceBasicBlock.tryBlocks) {

                TryBlock copiedTryBlock = new TryBlock();
                copiedTryBlock.exceptionHandler = sourceTryBlock.exceptionHandler;
                copiedTryBlock.firstBasicBlockOfTryBlock = sourceTryBlock.firstBasicBlockOfTryBlock;
                copiedTryBlock.lastBasicBlockOfTryBlock = sourceTryBlock.lastBasicBlockOfTryBlock;

                targetBasicBlock.tryBlocks.Add(copiedTryBlock);
            }


            // copy all handler blocks
            foreach (HandlerBlock sourceHandlerBlock in sourceBasicBlock.handlerBlocks) {

                HandlerBlock copiedHandlerBlock = new HandlerBlock();
                copiedHandlerBlock.exceptionHandler = sourceHandlerBlock.exceptionHandler;
                copiedHandlerBlock.typeOfHandler = sourceHandlerBlock.typeOfHandler;
                copiedHandlerBlock.firstBasicBlockOfHandlerBlock = sourceHandlerBlock.firstBasicBlockOfHandlerBlock;
                copiedHandlerBlock.lastBasicBlockOfHandlerBlock = sourceHandlerBlock.lastBasicBlockOfHandlerBlock;

                targetBasicBlock.handlerBlocks.Add(copiedHandlerBlock);
            }

        }


        // copies recursively the next basic block of the method CFG (with a try block branch as entry branch)
        private void copyMethodCfgRecursively(List<BasicBlock> processedBasicBlocks, BasicBlock sourceBasicBlock, ref BasicBlock targetBasicBlock, TryBlockTarget copiedEntryBranch) {

            // check if source basic block was already processed
            if (processedBasicBlocks.Contains(sourceBasicBlock)) {

                bool found = false;
                foreach (BasicBlock searchBasicBlock in this.basicBlocks) {
                    if (searchBasicBlock.id == sourceBasicBlock.id) {

                        // set entry branch target
                        copiedEntryBranch.takenTarget = searchBasicBlock;
                        searchBasicBlock.entryBranches.Add(copiedEntryBranch);

                        found = true;
                        break;
                    }
                }
                if (!found) {
                    throw new ArgumentException("Copied basic block was not found.");
                }

                return;
            }

            // add source basic block to the list of already processed basic blocks
            processedBasicBlocks.Add(sourceBasicBlock);

            // create copy of the source basic block
            targetBasicBlock = new BasicBlock();
            this.basicBlocks.Add(targetBasicBlock);

            // set entry branch target
            copiedEntryBranch.takenTarget = targetBasicBlock;
            targetBasicBlock.entryBranches.Add(copiedEntryBranch);

            // copy simple values
            targetBasicBlock.id = sourceBasicBlock.id;
            targetBasicBlock.semanticId = sourceBasicBlock.semanticId;
            targetBasicBlock.startIdx = sourceBasicBlock.startIdx;
            targetBasicBlock.endIdx = sourceBasicBlock.endIdx;

            // copy all operations of the basic block
            foreach (IOperation operation in sourceBasicBlock.operations) {
                InternalOperation copiedOperation = new InternalOperation();
                copiedOperation.OperationCode = operation.OperationCode;
                copiedOperation.Value = operation.Value;
                copiedOperation.Offset = operation.Offset;
                copiedOperation.Location = operation.Location;
                targetBasicBlock.operations.Add(copiedOperation);
            }


            // process rest of the cfg and copy the exit branch
            if ((sourceBasicBlock.exitBranch as NoBranchTarget) != null) {
                NoBranchTarget exitBranch = (sourceBasicBlock.exitBranch as NoBranchTarget);

                // create a copied exit branch
                NoBranchTarget copiedExitBranch = new NoBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as UnconditionalBranchTarget != null) {
                UnconditionalBranchTarget exitBranch = (sourceBasicBlock.exitBranch as UnconditionalBranchTarget);

                // create a copied exit branch
                UnconditionalBranchTarget copiedExitBranch = new UnconditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as ConditionalBranchTarget != null) {
                ConditionalBranchTarget exitBranch = (sourceBasicBlock.exitBranch as ConditionalBranchTarget);

                // create a copied exit branch
                ConditionalBranchTarget copiedExitBranch = new ConditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch, true);

                // process branch and next basic block
                nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.notTakenTarget, ref nextCopiedBasicBlock, copiedExitBranch, false);

            }

            else if (sourceBasicBlock.exitBranch as SwitchBranchTarget != null) {
                SwitchBranchTarget exitBranch = (sourceBasicBlock.exitBranch as SwitchBranchTarget);

                // create a copied exit branch
                SwitchBranchTarget copiedExitBranch = new SwitchBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // first process not taken branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.notTakenTarget, ref nextCopiedBasicBlock, copiedExitBranch, -1);

                // process all taken branches and next basic blocks
                for (int idx = 0; idx < exitBranch.takenTarget.Count(); idx++) {
                    nextCopiedBasicBlock = null;
                    this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget.ElementAt(idx), ref nextCopiedBasicBlock, copiedExitBranch, idx);
                }

            }

            else if (sourceBasicBlock.exitBranch as ExitBranchTarget != null) {

                // create a copied exit branch
                ExitBranchTarget copiedExitBranch = new ExitBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (sourceBasicBlock.exitBranch as ThrowBranchTarget != null) {

                // create a copied exit branch
                ThrowBranchTarget copiedExitBranch = new ThrowBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (sourceBasicBlock.exitBranch as TryBlockTarget != null) {
                TryBlockTarget exitBranch = (sourceBasicBlock.exitBranch as TryBlockTarget);

                // create a copied exit branch
                TryBlockTarget copiedExitBranch = new TryBlockTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as ExceptionBranchTarget != null) {
                ExceptionBranchTarget exitBranch = (sourceBasicBlock.exitBranch as ExceptionBranchTarget);

                // create a copied exit branch
                ExceptionBranchTarget copiedExitBranch = new ExceptionBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.exceptionTarget, ref nextCopiedBasicBlock, copiedExitBranch, true);

                // process branch and next basic block
                nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.exitTarget, ref nextCopiedBasicBlock, copiedExitBranch, false);

            }

            else {
                throw new ArgumentException("Do not know how to handle exit branch.");
            }


            // copy all try blocks
            foreach (TryBlock sourceTryBlock in sourceBasicBlock.tryBlocks) {

                TryBlock copiedTryBlock = new TryBlock();
                copiedTryBlock.exceptionHandler = sourceTryBlock.exceptionHandler;
                copiedTryBlock.firstBasicBlockOfTryBlock = sourceTryBlock.firstBasicBlockOfTryBlock;
                copiedTryBlock.lastBasicBlockOfTryBlock = sourceTryBlock.lastBasicBlockOfTryBlock;

                targetBasicBlock.tryBlocks.Add(copiedTryBlock);
            }


            // copy all handler blocks
            foreach (HandlerBlock sourceHandlerBlock in sourceBasicBlock.handlerBlocks) {

                HandlerBlock copiedHandlerBlock = new HandlerBlock();
                copiedHandlerBlock.exceptionHandler = sourceHandlerBlock.exceptionHandler;
                copiedHandlerBlock.typeOfHandler = sourceHandlerBlock.typeOfHandler;
                copiedHandlerBlock.firstBasicBlockOfHandlerBlock = sourceHandlerBlock.firstBasicBlockOfHandlerBlock;
                copiedHandlerBlock.lastBasicBlockOfHandlerBlock = sourceHandlerBlock.lastBasicBlockOfHandlerBlock;

                targetBasicBlock.handlerBlocks.Add(copiedHandlerBlock);
            }

        }


        // copies recursively the next basic block of the method CFG (with an exception branch as entry branch)
        private void copyMethodCfgRecursively(List<BasicBlock> processedBasicBlocks, BasicBlock sourceBasicBlock, ref BasicBlock targetBasicBlock, ExceptionBranchTarget copiedEntryBranch, bool exceptionEntryBranch) {

            // check if source basic block was already processed
            if (processedBasicBlocks.Contains(sourceBasicBlock)) {

                bool found = false;
                foreach (BasicBlock searchBasicBlock in this.basicBlocks) {
                    if (searchBasicBlock.id == sourceBasicBlock.id) {

                        // set entry branch target
                        if (exceptionEntryBranch) {
                            copiedEntryBranch.exceptionTarget = searchBasicBlock;

                        }
                        else {
                            copiedEntryBranch.exitTarget = searchBasicBlock;
                        }

                        searchBasicBlock.entryBranches.Add(copiedEntryBranch);
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    throw new ArgumentException("Copied basic block was not found.");
                }

                return;
            }

            // add source basic block to the list of already processed basic blocks
            processedBasicBlocks.Add(sourceBasicBlock);

            // create copy of the source basic block
            targetBasicBlock = new BasicBlock();
            this.basicBlocks.Add(targetBasicBlock);

            // set entry branch target
            if (exceptionEntryBranch) {
                copiedEntryBranch.exceptionTarget = targetBasicBlock;
            }
            else {
                copiedEntryBranch.exitTarget = targetBasicBlock;
            }
            targetBasicBlock.entryBranches.Add(copiedEntryBranch);

            // copy simple values
            targetBasicBlock.id = sourceBasicBlock.id;
            targetBasicBlock.semanticId = sourceBasicBlock.semanticId;
            targetBasicBlock.startIdx = sourceBasicBlock.startIdx;
            targetBasicBlock.endIdx = sourceBasicBlock.endIdx;

            // copy all operations of the basic block
            foreach (IOperation operation in sourceBasicBlock.operations) {
                InternalOperation copiedOperation = new InternalOperation();
                copiedOperation.OperationCode = operation.OperationCode;
                copiedOperation.Value = operation.Value;
                copiedOperation.Offset = operation.Offset;
                copiedOperation.Location = operation.Location;
                targetBasicBlock.operations.Add(copiedOperation);
            }


            // process rest of the cfg and copy the exit branch
            if ((sourceBasicBlock.exitBranch as NoBranchTarget) != null) {
                NoBranchTarget exitBranch = (sourceBasicBlock.exitBranch as NoBranchTarget);

                // create a copied exit branch
                NoBranchTarget copiedExitBranch = new NoBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as UnconditionalBranchTarget != null) {
                UnconditionalBranchTarget exitBranch = (sourceBasicBlock.exitBranch as UnconditionalBranchTarget);

                // create a copied exit branch
                UnconditionalBranchTarget copiedExitBranch = new UnconditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as ConditionalBranchTarget != null) {
                ConditionalBranchTarget exitBranch = (sourceBasicBlock.exitBranch as ConditionalBranchTarget);

                // create a copied exit branch
                ConditionalBranchTarget copiedExitBranch = new ConditionalBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch, true);

                // process branch and next basic block
                nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.notTakenTarget, ref nextCopiedBasicBlock, copiedExitBranch, false);

            }

            else if (sourceBasicBlock.exitBranch as SwitchBranchTarget != null) {
                SwitchBranchTarget exitBranch = (sourceBasicBlock.exitBranch as SwitchBranchTarget);

                // create a copied exit branch
                SwitchBranchTarget copiedExitBranch = new SwitchBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // first process not taken branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.notTakenTarget, ref nextCopiedBasicBlock, copiedExitBranch, -1);

                // process all taken branches and next basic blocks
                for (int idx = 0; idx < exitBranch.takenTarget.Count(); idx++) {
                    nextCopiedBasicBlock = null;
                    this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget.ElementAt(idx), ref nextCopiedBasicBlock, copiedExitBranch, idx);
                }

            }

            else if (sourceBasicBlock.exitBranch as ExitBranchTarget != null) {

                // create a copied exit branch
                ExitBranchTarget copiedExitBranch = new ExitBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (sourceBasicBlock.exitBranch as ThrowBranchTarget != null) {

                // create a copied exit branch
                ThrowBranchTarget copiedExitBranch = new ThrowBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

            }

            else if (sourceBasicBlock.exitBranch as TryBlockTarget != null) {
                TryBlockTarget exitBranch = (sourceBasicBlock.exitBranch as TryBlockTarget);

                // create a copied exit branch
                TryBlockTarget copiedExitBranch = new TryBlockTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.takenTarget, ref nextCopiedBasicBlock, copiedExitBranch);

            }

            else if (sourceBasicBlock.exitBranch as ExceptionBranchTarget != null) {
                ExceptionBranchTarget exitBranch = (sourceBasicBlock.exitBranch as ExceptionBranchTarget);

                // create a copied exit branch
                ExceptionBranchTarget copiedExitBranch = new ExceptionBranchTarget();
                copiedExitBranch.sourceBasicBlock = targetBasicBlock;
                targetBasicBlock.exitBranch = copiedExitBranch;

                // process branch and next basic block
                BasicBlock nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.exceptionTarget, ref nextCopiedBasicBlock, copiedExitBranch, true);

                // process branch and next basic block
                nextCopiedBasicBlock = null;
                this.copyMethodCfgRecursively(processedBasicBlocks, exitBranch.exitTarget, ref nextCopiedBasicBlock, copiedExitBranch, false);

            }

            else {
                throw new ArgumentException("Do not know how to handle exit branch.");
            }


            // copy all try blocks
            foreach (TryBlock sourceTryBlock in sourceBasicBlock.tryBlocks) {

                TryBlock copiedTryBlock = new TryBlock();
                copiedTryBlock.exceptionHandler = sourceTryBlock.exceptionHandler;
                copiedTryBlock.firstBasicBlockOfTryBlock = sourceTryBlock.firstBasicBlockOfTryBlock;
                copiedTryBlock.lastBasicBlockOfTryBlock = sourceTryBlock.lastBasicBlockOfTryBlock;

                targetBasicBlock.tryBlocks.Add(copiedTryBlock);
            }


            // copy all handler blocks
            foreach (HandlerBlock sourceHandlerBlock in sourceBasicBlock.handlerBlocks) {

                HandlerBlock copiedHandlerBlock = new HandlerBlock();
                copiedHandlerBlock.exceptionHandler = sourceHandlerBlock.exceptionHandler;
                copiedHandlerBlock.typeOfHandler = sourceHandlerBlock.typeOfHandler;
                copiedHandlerBlock.firstBasicBlockOfHandlerBlock = sourceHandlerBlock.firstBasicBlockOfHandlerBlock;
                copiedHandlerBlock.lastBasicBlockOfHandlerBlock = sourceHandlerBlock.lastBasicBlockOfHandlerBlock;

                targetBasicBlock.handlerBlocks.Add(copiedHandlerBlock);
            }

        }

    }


    // class represens the try block of an exception handler
    public class TryBlock {

        // the exception handler the try block belongs to
        public IOperationExceptionInformation exceptionHandler;

        // this flag is true when the try block starts inside this basic block
        // if it is false, then the basic block just resides somewhere inside the try block
        public bool firstBasicBlockOfTryBlock = false;

        // this flag is true when the try block ends inside this basic block
        // if it is false, then the basic block just resides somewhere inside the try block
        public bool lastBasicBlockOfTryBlock = false;
    }


    // class represens the handler block (catch/finally) of an exception handler
    public class HandlerBlock {

        // type of the handler block (catch/finally)
        public HandlerKind typeOfHandler;

        // the exception handler the handler block belongs to
        public IOperationExceptionInformation exceptionHandler;

        // this flag is true when the handler block starts inside this basic block
        // if it is false, then the basic block just resides somewhere inside the handler block
        public bool firstBasicBlockOfHandlerBlock = false;

        // this flag is true when the handler block ends inside this basic block
        // if it is false, then the basic block just resides somewhere inside the handler block
        public bool lastBasicBlockOfHandlerBlock = false;
    }


    // class represents a basic block used in a CFG
    public class BasicBlock {

        public BasicBlock(int semanticId = -1) {
            this.semanticId = semanticId;
            this.id = this.GetHashCode();
        }

        // a unique id of this basic block inside the CFG
        // NOTE: should only be manually set if the basic block represents a copy in a new CFG
        public int id;

        // semantic identifier of this basic block (when it is not important it is set to -1)
        // NOTE: not necessarily unique, a transformation can copy this basic block and transform it
        // the id is only unique for the semantics this basic block have
        public int semanticId = -1;

        // list of branches that entries this basic block
        public List<IBranchTarget> entryBranches = new List<IBranchTarget>();

        // branch that exits this basic block
        public IBranchTarget exitBranch;

        // instructions of the basic block 
        public List<IOperation> operations = new List<IOperation>();

        // the start index of the basic block (index of the list of instructions inside the CCI method)
        public int startIdx;

        // the end index of the basic block (index of the list of instructions inside the CCI method)
        public int endIdx;

        // list of try and handler blocks to which this basic block belongs
        public List<TryBlock> tryBlocks = new List<TryBlock>();
        public List<HandlerBlock> handlerBlocks = new List<HandlerBlock>();

        // list of metadata for the transformation of this basic block
        public List<ITransformationMetadata> transformationMetadata = new List<ITransformationMetadata>();
    }

}
