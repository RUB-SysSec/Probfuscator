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


    public class CfgManipulator {

        Log.Log logger = null;
        PeReader.DefaultHost host = null;
        IModule module = null;
        MethodCfg methodCfg = null;
        


        public CfgManipulator(IModule module, PeReader.DefaultHost host, Log.Log logger, MethodCfg methodCfg) {
            this.host = host;
            this.logger = logger;
            this.module = module;

            this.methodCfg = methodCfg;
        }


        // this function adds recursively all basic blocks given to the method cfg
        private void addBasicBlockToCfgRecursively(BasicBlock currentBasicBlock) {

            // check if the current basic block is set
            if (currentBasicBlock == null) {
                return;
            }

            // add the given basic block to the method cfg basic blocks
            // if it is already added => rest is also done
            if (!methodCfg.basicBlocks.Contains(currentBasicBlock)) {
                this.methodCfg.basicBlocks.Add(currentBasicBlock);
            }
            else {
                return;
            }

            // recursively add all other basic blocks of the new target code to the method cfg
            if (currentBasicBlock.exitBranch as NoBranchTarget != null) {
                BasicBlock tempBB = ((NoBranchTarget)currentBasicBlock.exitBranch).takenTarget;
                this.addBasicBlockToCfgRecursively(tempBB);
            }
            else if (currentBasicBlock.exitBranch as TryBlockTarget != null) {
                BasicBlock tempBB = ((TryBlockTarget)currentBasicBlock.exitBranch).takenTarget;
                this.addBasicBlockToCfgRecursively(tempBB);
            }
            else if (currentBasicBlock.exitBranch as UnconditionalBranchTarget != null) {
                BasicBlock tempBB = ((UnconditionalBranchTarget)currentBasicBlock.exitBranch).takenTarget;
                this.addBasicBlockToCfgRecursively(tempBB);
            }
            else if (currentBasicBlock.exitBranch as ConditionalBranchTarget != null) {
                BasicBlock tempBB = ((ConditionalBranchTarget)currentBasicBlock.exitBranch).takenTarget;
                this.addBasicBlockToCfgRecursively(tempBB);
                tempBB = ((ConditionalBranchTarget)currentBasicBlock.exitBranch).notTakenTarget;
                this.addBasicBlockToCfgRecursively(tempBB);
            }
            else if (currentBasicBlock.exitBranch as SwitchBranchTarget != null) {
                BasicBlock tempBB = ((SwitchBranchTarget)currentBasicBlock.exitBranch).notTakenTarget;
                this.addBasicBlockToCfgRecursively(tempBB);
                foreach (BasicBlock takenTempBB in ((SwitchBranchTarget)currentBasicBlock.exitBranch).takenTarget) {
                    this.addBasicBlockToCfgRecursively(takenTempBB);
                }
            }
            else if (currentBasicBlock.exitBranch as ExceptionBranchTarget != null) {
                BasicBlock tempBB = ((ExceptionBranchTarget)currentBasicBlock.exitBranch).exceptionTarget;
                this.addBasicBlockToCfgRecursively(tempBB);
                tempBB = ((ExceptionBranchTarget)currentBasicBlock.exitBranch).exitTarget;
                this.addBasicBlockToCfgRecursively(tempBB);
            }
        }


        // this function splits a basic block on the given index
        public void splitBasicBlock(BasicBlock firstPartBB, int splitIndex) {

            // check if the index is out of bounds
            if (splitIndex >= firstPartBB.operations.Count()) {
                throw new ArgumentException("Index is equal or greater than existing basic block operations.");
            }

            // check if there are at least 2 operations inside the basic block to split
            if (firstPartBB.operations.Count() < 2) {
                throw new ArgumentException("Too few operations in basic block for splitting.");
            }

            // create the new basic block that will become the second part of the basic block to split
            BasicBlock secondPartBB = new BasicBlock();

            // move the exit basic blocks to the new created basic block
            // and make the new created basic block the exit of the splitted one   
            secondPartBB.exitBranch = firstPartBB.exitBranch;
            secondPartBB.exitBranch.sourceBasicBlock = secondPartBB;
            NoBranchTarget tempExitBranch = new NoBranchTarget();
            tempExitBranch.takenTarget = secondPartBB;
            tempExitBranch.sourceBasicBlock = firstPartBB;
            firstPartBB.exitBranch = tempExitBranch;

            // add splitted basic block branch
            // to the entries of the new basic block
            secondPartBB.entryBranches.Add(tempExitBranch);

            // distribute the instructions to the basic blocks 
            List<IOperation> previousOperations = new List<IOperation>();
            List<IOperation> nextOperations = new List<IOperation>();
            for (int operationIdx = 0; operationIdx < firstPartBB.operations.Count(); operationIdx++) {

                if (operationIdx < splitIndex) {
                    previousOperations.Add(firstPartBB.operations.ElementAt(operationIdx));
                }
                else {
                    nextOperations.Add(firstPartBB.operations.ElementAt(operationIdx));
                }
            }
            firstPartBB.operations = previousOperations;
            secondPartBB.operations = nextOperations;

            
            // add a semantic id to the new basic block
            if (firstPartBB.semanticId == -1) {
                secondPartBB.semanticId = -1;
            }
            else {
                int highestSemanticId = 0;
                foreach (BasicBlock tempBB in methodCfg.basicBlocks) {
                    if (tempBB.semanticId > highestSemanticId) {
                        highestSemanticId = tempBB.semanticId;
                    }
                }

                secondPartBB.semanticId = highestSemanticId + 1;
            }


            // add new basic block to the cfg
            methodCfg.basicBlocks.Add(secondPartBB);

        }


        // this function adds a new local variable to the method
        public void addLocalVariable(ILocalDefinition localVariable) {

            ILGenerator ilGenerator = new ILGenerator(this.host, this.methodCfg.method);

            // add local variable
            List<ILocalDefinition> variableListCopy = new List<ILocalDefinition>(this.methodCfg.method.Body.LocalVariables);
            variableListCopy.Add(localVariable);

            // create the body
            List<ITypeDefinition> privateHelperTypesListCopy = new List<ITypeDefinition>(this.methodCfg.method.Body.PrivateHelperTypes);
            var newBody = new ILGeneratorMethodBody(ilGenerator, this.methodCfg.method.Body.LocalsAreZeroed, 8, this.methodCfg.method, variableListCopy, privateHelperTypesListCopy); // TODO dynamic max stack size?
            this.methodCfg.method.Body = newBody;

        }


        // this function replaces a basic block inside the cfg (oldBasicBlock) with a new basic block (newBasicBlock)
        public void replaceBasicBlock(BasicBlock oldBasicBlock, BasicBlock newBasicBlock) {
            this.replaceBasicBlock(oldBasicBlock, newBasicBlock, newBasicBlock);
        }


        // this function replaces a basic block inside the cfg (oldBasicBlock) with a construction of new basic blocks (newBasicBlockStart and newBasicBlockEnd)
        public void replaceBasicBlock(BasicBlock oldBasicBlock, BasicBlock newBasicBlockStart, BasicBlock newBasicBlockEnd) {

            // add new basic blocks to cfg
            this.addBasicBlockToCfgRecursively(newBasicBlockStart);


            // add entry branches that enter the old basic block to the new basic block
            newBasicBlockStart.entryBranches.AddRange(oldBasicBlock.entryBranches);

            // exchange the target of the entry branches from the old basic block to the new one
            foreach (IBranchTarget entryBranch in new List<IBranchTarget>(oldBasicBlock.entryBranches)) {

                if ((entryBranch as NoBranchTarget) != null) {

                    NoBranchTarget tempEntryBranch = (entryBranch as NoBranchTarget);

                    // check sanity of entry branch 
                    if (tempEntryBranch.takenTarget != oldBasicBlock) {
                        throw new ArgumentException("Entry branch must have old basic block as target.");
                    }

                    // set new basic block as target
                    tempEntryBranch.takenTarget = newBasicBlockStart;

                }
                else if ((entryBranch as UnconditionalBranchTarget) != null) {

                    UnconditionalBranchTarget tempEntryBranch = (entryBranch as UnconditionalBranchTarget);

                    // check sanity of entry branch
                    if (tempEntryBranch.takenTarget != oldBasicBlock) {
                        throw new ArgumentException("Entry branch must have old basic block as target.");
                    }

                    // set new basic block as target
                    tempEntryBranch.takenTarget = newBasicBlockStart;

                }
                else if ((entryBranch as ConditionalBranchTarget) != null) {

                    ConditionalBranchTarget tempEntryBranch = (entryBranch as ConditionalBranchTarget);
                    bool sanity = false;

                    // change all branches to the old basic block to the new basic block
                    if (tempEntryBranch.takenTarget == oldBasicBlock) {
                        tempEntryBranch.takenTarget = newBasicBlockStart;
                        sanity = true;
                    }
                    if (tempEntryBranch.notTakenTarget == oldBasicBlock) {
                        tempEntryBranch.notTakenTarget = newBasicBlockStart;
                        sanity = true;
                    }

                    // check sanity of entry branch
                    if (!sanity) {
                        throw new ArgumentException("Entry branch must have old basic block as target.");
                    }

                }
                else if ((entryBranch as SwitchBranchTarget) != null) {

                    SwitchBranchTarget tempEntryBranch = (entryBranch as SwitchBranchTarget);
                    bool sanity = false;

                    // change all branches to the old basic block to the new basic block
                    if (tempEntryBranch.notTakenTarget == oldBasicBlock) {
                        tempEntryBranch.notTakenTarget = newBasicBlockStart;
                        sanity = true;
                    }
                    for (int idx = 0; idx < tempEntryBranch.takenTarget.Count(); idx++) {
                        if (tempEntryBranch.takenTarget.ElementAt(idx) == oldBasicBlock) {
                            tempEntryBranch.takenTarget[idx] = newBasicBlockStart;
                            sanity = true;
                        }
                    }

                    // check sanity of entry branch
                    if (!sanity) {
                        throw new ArgumentException("Entry branch must have old basic block as target.");
                    }

                }
                else {
                    throw new ArgumentException("Not yet implemented.");
                }

                // remove entry branch from the old basic block entry branches list
                oldBasicBlock.entryBranches.Remove(entryBranch);

            }


            IBranchTarget exitBranch = oldBasicBlock.exitBranch;

            // change the exit branch of the old basic block to be the exit branch of the new one
            exitBranch.sourceBasicBlock = newBasicBlockEnd;
            newBasicBlockEnd.exitBranch = exitBranch;


            // check if the old basic block was the start basic block of the cfg
            // => change it to the new basic block
            if (this.methodCfg.startBasicBlock == oldBasicBlock) {
                this.methodCfg.startBasicBlock = newBasicBlockStart;
            }

            // remove old basic block if it still belongs to the cfg
            if (this.methodCfg.basicBlocks.Contains(oldBasicBlock)) {
                this.methodCfg.basicBlocks.Remove(oldBasicBlock);
            }

        }


        // ################################################### NO BRANCHES TO MANIPULATE ###################################################


        // insert the given new target basic block between the given no branch to manipulate
        public void insertBasicBlockBetweenBranch(NoBranchTarget branchToManipulate, BasicBlock newTarget, NoBranchTarget exitBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = branchToManipulate.takenTarget;

            this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref exitBranch.takenTarget);
        }


        // insert the given new target basic block between the given no branch to manipulate
        public void insertBasicBlockBetweenBranch(NoBranchTarget branchToManipulate, BasicBlock newTarget, UnconditionalBranchTarget exitBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = branchToManipulate.takenTarget;

            this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref exitBranch.takenTarget);
        }


        // insert the given new target basic block between the given no branch to manipulate
        public void insertBasicBlockBetweenBranch(NoBranchTarget branchToManipulate, BasicBlock newTarget, ConditionalBranchTarget exitBranch, bool useTakenBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = branchToManipulate.takenTarget;

            // check wether to use the taken or not taken part of the exit branch
            if (useTakenBranch) {
                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref exitBranch.takenTarget);
            }
            else {
                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref exitBranch.notTakenTarget);
            }
        }


        // insert the given new target basic block between the given conditional branch to manipulate
        public void insertBasicBlockBetweenBranch(NoBranchTarget branchToManipulate, BasicBlock newTarget, SwitchBranchTarget exitBranch, int switchTakenBranchIdx) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = branchToManipulate.takenTarget;
            BasicBlock tempTakenTarget = null;

            this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref tempTakenTarget);

            // fill switch taken branch list with null when index is out of range
            while (exitBranch.takenTarget.Count() <= switchTakenBranchIdx) {
                exitBranch.takenTarget.Add(null);
            }

            exitBranch.takenTarget[switchTakenBranchIdx] = tempTakenTarget;

        }


        // insert the given new target basic block (given by start and end block) between the given no branch to manipulate
        public void insertBasicBlockBetweenBranch(NoBranchTarget branchToManipulate, BasicBlock newTargetStart, BasicBlock newTargetEnd, NoBranchTarget exitBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = branchToManipulate.takenTarget;

            this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTargetStart, newTargetEnd, exitBranch, ref exitBranch.takenTarget);
        }


        // insert the given new target basic block (given by start and end block) between the given no branch to manipulate
        public void insertBasicBlockBetweenBranch(NoBranchTarget branchToManipulate, BasicBlock newTargetStart, BasicBlock newTargetEnd, UnconditionalBranchTarget exitBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = branchToManipulate.takenTarget;

            this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTargetStart, newTargetEnd, exitBranch, ref exitBranch.takenTarget);
        }


        // ################################################### UNCONDITIONAL BRANCHES TO MANIPULATE ###################################################


        // insert the given new target basic block between the given unconditional branch to manipulate
        public void insertBasicBlockBetweenBranch(UnconditionalBranchTarget branchToManipulate, BasicBlock newTarget, NoBranchTarget exitBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = branchToManipulate.takenTarget;

            this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref exitBranch.takenTarget);
        }


        // insert the given new target basic block between the given unconditional branch to manipulate
        public void insertBasicBlockBetweenBranch(UnconditionalBranchTarget branchToManipulate, BasicBlock newTarget, UnconditionalBranchTarget exitBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = branchToManipulate.takenTarget;

            this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref exitBranch.takenTarget);
        }


        // insert the given new target basic block between the given unconditional branch to manipulate
        public void insertBasicBlockBetweenBranch(UnconditionalBranchTarget branchToManipulate, BasicBlock newTarget, ConditionalBranchTarget exitBranch, bool useTakenBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = branchToManipulate.takenTarget;

            // check wether to use the taken or not taken part of the exit branch
            if (useTakenBranch) {
                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref exitBranch.takenTarget);
            }
            else {
                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref exitBranch.notTakenTarget);
            }
        }


        // insert the given new target basic block between the given conditional branch to manipulate
        public void insertBasicBlockBetweenBranch(UnconditionalBranchTarget branchToManipulate, BasicBlock newTarget, SwitchBranchTarget exitBranch, int switchTakenBranchIdx) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = branchToManipulate.takenTarget;
            BasicBlock tempTakenTarget = null;

            this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref tempTakenTarget);

            // fill switch taken branch list with null when index is out of range
            while (exitBranch.takenTarget.Count() <= switchTakenBranchIdx) {
                exitBranch.takenTarget.Add(null);
            }

            exitBranch.takenTarget[switchTakenBranchIdx] = tempTakenTarget;

        }


        // insert the given new target basic block (given by start and end block) between the given unconditional branch to manipulate
        public void insertBasicBlockBetweenBranch(UnconditionalBranchTarget branchToManipulate, BasicBlock newTargetStart, BasicBlock newTargetEnd, NoBranchTarget exitBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = branchToManipulate.takenTarget;

            this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTargetStart, newTargetEnd, exitBranch, ref exitBranch.takenTarget);
        }


        // insert the given new target basic block (given by start and end block) between the given unconditional branch to manipulate
        public void insertBasicBlockBetweenBranch(UnconditionalBranchTarget branchToManipulate, BasicBlock newTargetStart, BasicBlock newTargetEnd, UnconditionalBranchTarget exitBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = branchToManipulate.takenTarget;

            this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTargetStart, newTargetEnd, exitBranch, ref exitBranch.takenTarget);
        }


        // ################################################### CONDITIONAL BRANCHES TO MANIPULATE ###################################################


        // insert the given new target basic block between the given conditional branch to manipulate
        public void insertBasicBlockBetweenBranch(ConditionalBranchTarget branchToManipulate, bool manipulateTakenBranch, BasicBlock newTarget, NoBranchTarget exitBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = null;

            // manipulate the correct branch
            if (manipulateTakenBranch) {
                outroBasicBlock = branchToManipulate.takenTarget;
                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref exitBranch.takenTarget);
            }
            else {
                outroBasicBlock = branchToManipulate.notTakenTarget;
                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.notTakenTarget, newTarget, newTarget, exitBranch, ref exitBranch.takenTarget);
            }
        }


        // insert the given new target basic block between the given conditional branch to manipulate
        public void insertBasicBlockBetweenBranch(ConditionalBranchTarget branchToManipulate, bool manipulateTakenBranch, BasicBlock newTarget, UnconditionalBranchTarget exitBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = null;

            // manipulate the correct branch
            if (manipulateTakenBranch) {
                outroBasicBlock = branchToManipulate.takenTarget;
                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref exitBranch.takenTarget);
            }
            else {
                outroBasicBlock = branchToManipulate.notTakenTarget;
                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.notTakenTarget, newTarget, newTarget, exitBranch, ref exitBranch.takenTarget);
            }
        }


        // insert the given new target basic block between the given conditional branch to manipulate
        public void insertBasicBlockBetweenBranch(ConditionalBranchTarget branchToManipulate, bool manipulateTakenBranch, BasicBlock newTarget, ConditionalBranchTarget exitBranch, bool useTakenBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = null;

            // check wether to use the taken or not taken part of the exit branch
            if (useTakenBranch) {

                // check wether to manipulate the taken or not taken branch of the branch to manipulate
                if (manipulateTakenBranch) {
                    outroBasicBlock = branchToManipulate.takenTarget;
                    this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref exitBranch.takenTarget);
                }
                else {
                    outroBasicBlock = branchToManipulate.notTakenTarget;
                    this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.notTakenTarget, newTarget, newTarget, exitBranch, ref exitBranch.takenTarget);
                }


            }
            else {

                // check wether to manipulate the taken or not taken branch of the branch to manipulate
                if (manipulateTakenBranch) {
                    outroBasicBlock = branchToManipulate.takenTarget;
                    this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref exitBranch.notTakenTarget);
                }
                else {
                    outroBasicBlock = branchToManipulate.notTakenTarget;
                    this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.notTakenTarget, newTarget, newTarget, exitBranch, ref exitBranch.notTakenTarget);
                }
            }
        }


        // insert the given new target basic block between the given conditional branch to manipulate
        public void insertBasicBlockBetweenBranch(ConditionalBranchTarget branchToManipulate, bool manipulateTakenBranch, BasicBlock newTarget, SwitchBranchTarget exitBranch, int switchTakenBranchIdx) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = null;

            // check wether to manipulate the taken or not taken branch of the branch to manipulate
            if (manipulateTakenBranch) {
                outroBasicBlock = branchToManipulate.takenTarget;
                BasicBlock tempTakenTarget = null;
                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTarget, newTarget, exitBranch, ref tempTakenTarget);

                // fill switch taken branch list with null when index is out of range
                while (exitBranch.takenTarget.Count() <= switchTakenBranchIdx) {
                    exitBranch.takenTarget.Add(null);
                }

                exitBranch.takenTarget[switchTakenBranchIdx] = tempTakenTarget;
            }
            else {
                BasicBlock tempTakenTarget = null;
                outroBasicBlock = branchToManipulate.notTakenTarget;
                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.notTakenTarget, newTarget, newTarget, exitBranch, ref tempTakenTarget);

                // fill switch taken branch list with null when index is out of range
                while (exitBranch.takenTarget.Count() <= switchTakenBranchIdx) {
                    exitBranch.takenTarget.Add(null);
                }

                exitBranch.takenTarget[switchTakenBranchIdx] = tempTakenTarget;
            }

        }


        // insert the given new target basic block (given by start and end block) between the given conditional branch to manipulate
        public void insertBasicBlockBetweenBranch(ConditionalBranchTarget branchToManipulate, bool manipulateTakenBranch, BasicBlock newTargetStart, BasicBlock newTargetEnd, NoBranchTarget exitBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = null;

            // manipulate the correct branch
            if (manipulateTakenBranch) {
                outroBasicBlock = branchToManipulate.takenTarget;
                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTargetStart, newTargetEnd, exitBranch, ref exitBranch.takenTarget);
            }
            else {
                outroBasicBlock = branchToManipulate.notTakenTarget;
                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.notTakenTarget, newTargetStart, newTargetEnd, exitBranch, ref exitBranch.takenTarget);
            }
        }


        // insert the given new target basic block (given by start and end block) between the given conditional branch to manipulate
        public void insertBasicBlockBetweenBranch(ConditionalBranchTarget branchToManipulate, bool manipulateTakenBranch, BasicBlock newTargetStart, BasicBlock newTargetEnd, UnconditionalBranchTarget exitBranch) {
            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = null;

            // manipulate the correct branch
            if (manipulateTakenBranch) {
                outroBasicBlock = branchToManipulate.takenTarget;
                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.takenTarget, newTargetStart, newTargetEnd, exitBranch, ref exitBranch.takenTarget);
            }
            else {
                outroBasicBlock = branchToManipulate.notTakenTarget;
                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulate.notTakenTarget, newTargetStart, newTargetEnd, exitBranch, ref exitBranch.takenTarget);
            }
        }


        // ################################################### SWITCH BRANCHES TO MANIPULATE ###################################################


        // insert the given new target basic block between the given switch branch to manipulate
        public void insertBasicBlockBetweenBranch(SwitchBranchTarget branchToManipulate, int manipulateTakenBranchIdx, BasicBlock newTarget, UnconditionalBranchTarget exitBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = null;


            outroBasicBlock = branchToManipulate.takenTarget.ElementAt(manipulateTakenBranchIdx);
            BasicBlock tempTakenTarget = null;

            this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref tempTakenTarget, newTarget, newTarget, exitBranch, ref exitBranch.takenTarget);

            branchToManipulate.takenTarget[manipulateTakenBranchIdx] = tempTakenTarget;
        }


        // insert the given new target basic block between the given switch branch to manipulate
        public void insertBasicBlockBetweenBranch(SwitchBranchTarget branchToManipulate, int manipulateTakenBranchIdx, BasicBlock newTarget, ConditionalBranchTarget exitBranch, bool useTakenBranch) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = null;

            // check wether to use the taken or not taken part of the exit branch
            if (useTakenBranch) {

                outroBasicBlock = branchToManipulate.takenTarget.ElementAt(manipulateTakenBranchIdx);
                BasicBlock tempTakenTarget = null;

                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref tempTakenTarget, newTarget, newTarget, exitBranch, ref exitBranch.takenTarget);

                branchToManipulate.takenTarget[manipulateTakenBranchIdx] = tempTakenTarget;
            }
            else {

                outroBasicBlock = branchToManipulate.takenTarget.ElementAt(manipulateTakenBranchIdx);
                BasicBlock tempTakenTarget = null;

                this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref tempTakenTarget, newTarget, newTarget, exitBranch, ref exitBranch.notTakenTarget);

                branchToManipulate.takenTarget[manipulateTakenBranchIdx] = tempTakenTarget;
            }
        }


        // insert the given new target basic block between the given switch branch to manipulate
        public void insertBasicBlockBetweenBranch(SwitchBranchTarget branchToManipulate, int manipulateTakenBranchIdx, BasicBlock newTarget, SwitchBranchTarget exitBranch, int switchTakenBranchIdx) {

            // get the basic block from that the branch is taken and the basic block to that the branch is taken
            BasicBlock introBasicBlock = branchToManipulate.sourceBasicBlock;
            BasicBlock outroBasicBlock = null;

            outroBasicBlock = branchToManipulate.takenTarget.ElementAt(manipulateTakenBranchIdx);
            BasicBlock branchToManipulateTakenTarget = null;
            BasicBlock exitBranchTakenTarget = null;

            this.insertBasicBlockBetweenBranch(introBasicBlock, outroBasicBlock, branchToManipulate, ref branchToManipulateTakenTarget, newTarget, newTarget, exitBranch, ref exitBranchTakenTarget);

            // fill switch taken branch list with null when index is out of range
            while (exitBranch.takenTarget.Count() <= switchTakenBranchIdx) {
                exitBranch.takenTarget.Add(null);
            }

            exitBranch.takenTarget[switchTakenBranchIdx] = exitBranchTakenTarget;
            branchToManipulate.takenTarget[manipulateTakenBranchIdx] = branchToManipulateTakenTarget;

        }


        // ################################################### BRANCH MANIPULATION LOGIC ###################################################


        // insert the given new target basic block (given by start and end block) between the given branch to manipulate
        private void insertBasicBlockBetweenBranch(BasicBlock introBasicBlock, BasicBlock outroBasicBlock, IBranchTarget branchToManipulate, ref BasicBlock branchToManipulateTargetBasicBlock, BasicBlock newTargetStart, BasicBlock newTargetEnd, IBranchTarget exitBranch, ref BasicBlock exitBranchTargetBasicBlock) {

            // check if there exists try/handler blocks
            if (introBasicBlock.tryBlocks.Count() != 0) {
                throw new ArgumentException("Try blocks not yet implemented.");
            }
            if (introBasicBlock.handlerBlocks.Count() != 0) {
                throw new ArgumentException("Handler blocks not yet implemented.");
            }

            // add the new target given by basic blocks to the method cfg basic blocks
            if (!methodCfg.basicBlocks.Contains(newTargetStart)) {
                this.methodCfg.basicBlocks.Add(newTargetStart);
            }

            // recursively add all other basic blocks of the new target code to the method cfg
            if (newTargetStart.exitBranch as NoBranchTarget != null) {
                if (newTargetStart != newTargetEnd) {
                    BasicBlock tempBB = ((NoBranchTarget)newTargetStart.exitBranch).takenTarget;
                    this.addBasicBlockToCfgRecursively(tempBB);
                }
            }
            else if (newTargetStart.exitBranch as TryBlockTarget != null) {
                if (newTargetStart != newTargetEnd) {
                    BasicBlock tempBB = ((TryBlockTarget)newTargetStart.exitBranch).takenTarget;
                    this.addBasicBlockToCfgRecursively(tempBB);
                }
            }
            else if (newTargetStart.exitBranch as UnconditionalBranchTarget != null) {
                if (newTargetStart != newTargetEnd) {
                    BasicBlock tempBB = ((UnconditionalBranchTarget)newTargetStart.exitBranch).takenTarget;
                    this.addBasicBlockToCfgRecursively(tempBB);
                }
            }
            else if (newTargetStart.exitBranch as ConditionalBranchTarget != null) {
                BasicBlock tempBB = ((ConditionalBranchTarget)newTargetStart.exitBranch).takenTarget;
                this.addBasicBlockToCfgRecursively(tempBB);
                tempBB = ((ConditionalBranchTarget)newTargetStart.exitBranch).notTakenTarget;
                this.addBasicBlockToCfgRecursively(tempBB);
            }
            else if (newTargetStart.exitBranch as SwitchBranchTarget != null) {
                BasicBlock tempBB = ((SwitchBranchTarget)newTargetStart.exitBranch).notTakenTarget;
                this.addBasicBlockToCfgRecursively(tempBB);
                foreach (BasicBlock takenTempBB in ((SwitchBranchTarget)newTargetStart.exitBranch).takenTarget) {
                    this.addBasicBlockToCfgRecursively(takenTempBB);
                }
            }
            else if (newTargetStart.exitBranch as ExceptionBranchTarget != null) {
                BasicBlock tempBB = ((ExceptionBranchTarget)newTargetStart.exitBranch).exceptionTarget;
                this.addBasicBlockToCfgRecursively(tempBB);
                tempBB = ((ExceptionBranchTarget)newTargetStart.exitBranch).exitTarget;
                this.addBasicBlockToCfgRecursively(tempBB);
            }
            else {
                throw new ArgumentException("Do not know how to handle branch target in order to add all basic blocks to the method CFG.");
            }

            // add the branch to manipulate as the entry branch of the new target basic block
            newTargetStart.entryBranches.Add(branchToManipulate);

            // remove branch entry from old target
            outroBasicBlock.entryBranches.Remove(branchToManipulate);

            // make the given target basic block the new branch target
            branchToManipulateTargetBasicBlock = newTargetStart;

            // set branch as exit branch of the new last basic block of the new target code
            newTargetEnd.exitBranch = exitBranch;

            // set the last basic block of the new target as source of the exit branch
            exitBranch.sourceBasicBlock = newTargetEnd;
            exitBranchTargetBasicBlock = outroBasicBlock;

            // add branch to the list of entry branches of the original outro basic block
            // (if the exit branch of the new code is a no branch target => check if it is the only one that entries the outro basic block)
            if ((exitBranch as NoBranchTarget) != null) {
                foreach (IBranchTarget temp in outroBasicBlock.entryBranches) {
                    if ((temp as NoBranchTarget) != null) {
                        throw new ArgumentException("Only one 'No Branch Target' can be entry of a basic block.");
                    }
                }
            }
            outroBasicBlock.entryBranches.Add(exitBranch);

        }

    }

}
