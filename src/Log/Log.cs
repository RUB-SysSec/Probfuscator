using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Cci;
using Microsoft.Cci.MutableCodeModel;
using CfgElements;
using GraphElements;
using TransformationsMetadata;

namespace Log {

    public class Log {

        String logPath;
        System.IO.StreamWriter file;

        public Log(String logPath, String mainLogfile) {
            this.logPath = logPath;
            this.file = new System.IO.StreamWriter(this.logPath + "\\" + mainLogfile);
        }

        ~Log() {
            try {
                this.file.Close();
            }
            catch {
            }
        }


        public String sanitizeString(String inputStr) {
            String invalidChars = " ?&^$#@!()+-,:;<>’\'-*|{}[]";
            String outputStr = "";
            foreach (char chr in inputStr) {
                if (invalidChars.Contains(chr)) {
                    outputStr += "_";
                }
                else {
                    outputStr += chr;
                }
            }
            return outputStr;
        }


        public void writeLine(String line) {
            this.file.WriteLine(line);
            this.file.Flush();
        }

        public String makeFuncSigString(IMethodDefinition method) {
            String logMethodName = method.Name.ToString() + "(";
            bool first = true;
            foreach (IParameterDefinition tempParameter in method.Parameters) {
                if (first) {
                    first = false;
                }
                else {
                    logMethodName += ", ";
                }
                logMethodName += tempParameter.Type.ToString();
            }
            logMethodName += ") (key " + method.InternedKey.ToString() + ")";
            return logMethodName;
        }


        public void writeAllMethodKeys(NamedTypeDefinition classToWrite) {
            if (classToWrite.Methods != null) {
                foreach (MethodDefinition method in classToWrite.Methods) {
                    this.writeLine(this.makeFuncSigString(method));
                }
            }
        }


        public void dumpMethodCfg(MethodCfg methodCfg, String fileName) {

            // shorten file name if it is too long
            fileName = fileName.Length >= 230 ? fileName.Substring(0, 230) : fileName;

            // dump cfg created from the exit branches
            System.IO.StreamWriter dotFile = new System.IO.StreamWriter(this.logPath + "\\exitBranches_" + fileName + ".dot");

            // start .dot file graph
            dotFile.WriteLine("digraph G {");

            // write all basic blocks to .dot file
            for (int idx = 0; idx < methodCfg.basicBlocks.Count(); idx++) {

                BasicBlock currentBasicBlock = methodCfg.basicBlocks.ElementAt(idx);

                // write the current basic block to the file and all its instructions
                dotFile.WriteLine("BB" + idx.ToString() + " [shape=record]");
                bool first = true;
                for (int opIdx = 0; opIdx < currentBasicBlock.operations.Count(); opIdx++) {
                    var operation = currentBasicBlock.operations.ElementAt(opIdx);
                    if (first) {
                        dotFile.Write("BB" + idx.ToString() + " [label=\"{");
                        dotFile.Write("NAME: BB" + idx.ToString() + "|");
                        dotFile.Write("ID: " + currentBasicBlock.id.ToString() + "|");
                        dotFile.Write("SEMANTIC ID: " + currentBasicBlock.semanticId.ToString() + "|");
                        first = false;

                        // dump basic block transformation metadata
                        foreach (ITransformationMetadata metadata in currentBasicBlock.transformationMetadata) {
                            if ((metadata as GraphTransformerMetadataBasicBlock) != null) {

                                GraphTransformerMetadataBasicBlock temp = (metadata as GraphTransformerMetadataBasicBlock);
                                String validPathIdString = "";
                                for (int linkIdx = 0; linkIdx < temp.correspondingGraphNodes.Count(); linkIdx++) {
                                    validPathIdString += temp.correspondingGraphNodes.ElementAt(linkIdx).validPathId.ToString() + ";";
                                }

                                dotFile.Write("GRAPH_BLOCK (valid ID: " + validPathIdString + ")|");

                                // dump position of the element in the graph (for each valid path)
                                for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                    String positionString = "";
                                    foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                        positionString += position.ToString() + ";";
                                    }
                                    dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                }

                                // dump the valid and invalid interfaces of the graph element (for each valid path)
                                for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {

                                    PathElement pathElement = null;
                                    foreach (PathElement tempElement in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.pathElements) {
                                        if (tempElement.validPathId == temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId) {
                                            pathElement = tempElement;
                                            break;
                                        }
                                    }
                                    if (pathElement == null) {
                                        throw new ArgumentNullException("No path element for valid graph id was found.");
                                    }


                                    String tempOutput = "";
                                    foreach (ITypeReference interfaceInList in pathElement.validInterfaces) {
                                        tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                                    }
                                    dotFile.Write("valid Interfaces: " + tempOutput + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");

                                    tempOutput = "";
                                    foreach (ITypeReference interfaceInList in pathElement.invalidInterfaces) {
                                        tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                                    }
                                    dotFile.Write("invalid Interfaces: " + tempOutput + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                }

                            }
                            else if ((metadata as GraphTransformerPredicateBasicBlock) != null) {

                                GraphTransformerPredicateBasicBlock temp = (metadata as GraphTransformerPredicateBasicBlock);
                                String validPathIdString = "";
                                for (int linkIdx = 0; linkIdx < temp.correspondingGraphNodes.Count(); linkIdx++) {
                                    validPathIdString += temp.correspondingGraphNodes.ElementAt(linkIdx).validPathId.ToString() + ";";
                                }

                                if (temp.predicateType == GraphOpaquePredicate.True) {
                                    dotFile.Write("TRUE_PREDICATE_BLOCK (valid ID: " + validPathIdString + ")|");

                                    // dump position of the element in the graph (for each valid path)
                                    for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                        String positionString = "";
                                        foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                            positionString += position.ToString() + ";";
                                        }
                                        dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                    }
                                }
                                else if (temp.predicateType == GraphOpaquePredicate.False) {
                                    dotFile.Write("FALSE_PREDICATE_BLOCK (valid ID: " + validPathIdString + ")|");

                                    // dump position of the element in the graph (for each valid path)
                                    for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                        String positionString = "";
                                        foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                            positionString += position.ToString() + ";";
                                        }
                                        dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                    }
                                }
                                else if (temp.predicateType == GraphOpaquePredicate.Random) {
                                    dotFile.Write("RANDOM_PREDICATE_BLOCK (valid ID: " + validPathIdString + ")|");

                                    // dump position of the element in the graph (for each valid path)
                                    for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                        String positionString = "";
                                        foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                            positionString += position.ToString() + ";";
                                        }
                                        dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                    }
                                }
                                else {
                                    throw new ArgumentException("Do not know how to handle opaque predicate type.");
                                }
                            }
                            else if ((metadata as GraphTransformerNextNodeBasicBlock) != null) {

                                GraphTransformerNextNodeBasicBlock temp = (metadata as GraphTransformerNextNodeBasicBlock);
                                String validPathIdString = "";
                                for (int linkIdx = 0; linkIdx < temp.correspondingGraphNodes.Count(); linkIdx++) {
                                    validPathIdString += temp.correspondingGraphNodes.ElementAt(linkIdx).validPathId.ToString() + ";";
                                }

                                if (temp.correctNextNode) {
                                    dotFile.Write("CORRECT_NEXT_NODE_BLOCK (valid ID: " + validPathIdString + ")|");

                                    // dump position of the element in the graph (for each valid path)
                                    for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                        String positionString = "";
                                        foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                            positionString += position.ToString() + ";";
                                        }
                                        dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                    }
                                }
                                else {
                                    dotFile.Write("WRONG_NEXT_NODE_BLOCK (valid ID: " + validPathIdString + ")|");

                                    // dump position of the element in the graph (for each valid path)
                                    for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                        String positionString = "";
                                        foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                            positionString += position.ToString() + ";";
                                        }
                                        dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                    }
                                }
                            }
                            else if ((metadata as GraphTransformerDeadCodeBasicBlock) != null) {

                                GraphTransformerDeadCodeBasicBlock temp = (metadata as GraphTransformerDeadCodeBasicBlock);

                                dotFile.Write("DEAD_CODE_BLOCK (template semantic ID: " + temp.semanticId.ToString() + ")|");
                            }
                            else if ((metadata as GraphTransformerStateBasicBlock) != null) {

                                GraphTransformerStateBasicBlock temp = (metadata as GraphTransformerStateBasicBlock);
                                String validPathIdString = "";
                                for (int linkIdx = 0; linkIdx < temp.correspondingGraphNodes.Count(); linkIdx++) {
                                    validPathIdString += temp.correspondingGraphNodes.ElementAt(linkIdx).validPathId.ToString() + ";";
                                }

                                dotFile.Write("STATE_BLOCK (valid ID: " + validPathIdString + ")|");

                                // dump position of the element in the graph (for each valid path)
                                for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                    String positionString = "";
                                    foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                        positionString += position.ToString() + ";";
                                    }
                                    dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                }
                            }
                            else if ((metadata as GraphTransformerStateChangeBasicBlock) != null) {

                                GraphTransformerStateChangeBasicBlock temp = (metadata as GraphTransformerStateChangeBasicBlock);
                                String validPathIdString = "";
                                for (int linkIdx = 0; linkIdx < temp.correspondingGraphNodes.Count(); linkIdx++) {
                                    validPathIdString += temp.correspondingGraphNodes.ElementAt(linkIdx).validPathId.ToString() + ";";
                                }

                                dotFile.Write("STATE_CHANGE_BLOCK (valid ID: " + validPathIdString + ")|");

                                // dump position of the element in the graph (for each valid path)
                                for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                    String positionString = "";
                                    foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                        positionString += position.ToString() + ";";
                                    }
                                    dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                }
                            }
                            else if ((metadata as GraphTransformerIntermediateBasicBlock) != null) {

                                GraphTransformerIntermediateBasicBlock temp = (metadata as GraphTransformerIntermediateBasicBlock);
                                String validPathIdString = "";
                                for (int linkIdx = 0; linkIdx < temp.correspondingGraphNodes.Count(); linkIdx++) {
                                    validPathIdString += temp.correspondingGraphNodes.ElementAt(linkIdx).validPathId.ToString() + ";";
                                }

                                dotFile.Write("INTERMEDIATE_BASIC_BLOCK (valid ID: " + validPathIdString + ")|");

                                // dump position of the element in the graph (for each valid path)
                                for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                    String positionString = "";
                                    foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                        positionString += position.ToString() + ";";
                                    }
                                    dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                }

                            }
                            else if ((metadata as DevBasicBlock) != null) {

                                DevBasicBlock temp = (metadata as DevBasicBlock);

                                dotFile.Write("DEV_NOTE: " + temp.note + "|");
                            }
                            else {
                                throw new ArgumentException("Do not know how to handle transformation metadata.");
                            }
                        }

                    }
                    else {
                        dotFile.Write("|");
                    }

                    // insert try block beginnings
                    foreach (var tryBlock in currentBasicBlock.tryBlocks) {
                        if (tryBlock.firstBasicBlockOfTryBlock && opIdx == 0) {
                            dotFile.Write("TRY START (" + this.sanitizeString(tryBlock.exceptionHandler.ExceptionType.ToString()) + ")|");
                        }
                    }

                    // insert catch block beginnings
                    foreach (var handlerBlock in currentBasicBlock.handlerBlocks) {
                        if (handlerBlock.firstBasicBlockOfHandlerBlock && opIdx == 0) {
                            if (handlerBlock.typeOfHandler == HandlerKind.Catch) {
                                dotFile.Write("CATCH START (" + this.sanitizeString(handlerBlock.exceptionHandler.ExceptionType.ToString()) + ")|");
                            }
                            else if (handlerBlock.typeOfHandler == HandlerKind.Finally) {
                                dotFile.Write("FINALLY START (" + this.sanitizeString(handlerBlock.exceptionHandler.ExceptionType.ToString()) + ")|");
                            }
                            else {
                                throw new ArgumentException("Do not know how to handle handler.");
                            }
                        }
                    }

                    // check if instruction has an argument
                    if (operation.Value != null) {
                        dotFile.Write(operation.OperationCode.ToString() + " " + this.sanitizeString(operation.Value.ToString()));
                    }
                    else {
                        dotFile.Write(operation.OperationCode.ToString());
                    }

                    // insert try block endings
                    foreach (var tryBlock in currentBasicBlock.tryBlocks) {
                        if (tryBlock.lastBasicBlockOfTryBlock && (currentBasicBlock.operations.Count() - 1) == opIdx) {
                            dotFile.Write("|TRY END (" + this.sanitizeString(tryBlock.exceptionHandler.ExceptionType.ToString()) + ")");
                        }
                    }

                    // insert catch/finally block endings
                    foreach (var handlerBlock in currentBasicBlock.handlerBlocks) {
                        if (handlerBlock.lastBasicBlockOfHandlerBlock && (currentBasicBlock.operations.Count() - 1) == opIdx) {
                            if (handlerBlock.typeOfHandler == HandlerKind.Catch) {
                                dotFile.Write("|CATCH END (" + this.sanitizeString(handlerBlock.exceptionHandler.ExceptionType.ToString()) + ")");
                            }
                            else if (handlerBlock.typeOfHandler == HandlerKind.Finally) {
                                dotFile.Write("|FINALLY END (" + this.sanitizeString(handlerBlock.exceptionHandler.ExceptionType.ToString()) + ")");
                            }
                            else {
                                throw new ArgumentException("Do not know how to handle handler.");
                            }
                        }
                    }
                }
                dotFile.WriteLine("}\"]");

                // write all the exits of the basic block to the file
                if (methodCfg.basicBlocks.ElementAt(idx).exitBranch as NoBranchTarget != null) {
                    NoBranchTarget tempBranch = (methodCfg.basicBlocks.ElementAt(idx).exitBranch as NoBranchTarget);

                    // search index of exit basic block in cfg
                    for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                        if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.takenTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"blue\" ]");
                            break;
                        }
                    }
                }
                else if (methodCfg.basicBlocks.ElementAt(idx).exitBranch as TryBlockTarget != null) {
                    TryBlockTarget tempBranch = (methodCfg.basicBlocks.ElementAt(idx).exitBranch as TryBlockTarget);

                    // search index of exit basic block in cfg
                    for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                        if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.takenTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"blue\" ]");
                            break;
                        }
                    }
                }
                else if (methodCfg.basicBlocks.ElementAt(idx).exitBranch as UnconditionalBranchTarget != null) {
                    UnconditionalBranchTarget tempBranch = (methodCfg.basicBlocks.ElementAt(idx).exitBranch as UnconditionalBranchTarget);

                    // search index of exit basic block in cfg
                    for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                        if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.takenTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"blue\" ]");
                            break;
                        }
                    }
                }
                else if (methodCfg.basicBlocks.ElementAt(idx).exitBranch as ConditionalBranchTarget != null) {
                    ConditionalBranchTarget tempBranch = (methodCfg.basicBlocks.ElementAt(idx).exitBranch as ConditionalBranchTarget);

                    // search index of exit basic block in cfg
                    bool takenTargetFound = false;
                    bool notTakenTargetFound = false;
                    for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                        if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.takenTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"green\" ]");
                            takenTargetFound = true;
                        }
                        else if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.notTakenTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"red\" ]");
                            notTakenTargetFound = true;
                        }
                        else if (takenTargetFound && notTakenTargetFound) {
                            break;
                        }
                    }
                }
                else if (methodCfg.basicBlocks.ElementAt(idx).exitBranch as SwitchBranchTarget != null) {
                    SwitchBranchTarget tempBranch = (methodCfg.basicBlocks.ElementAt(idx).exitBranch as SwitchBranchTarget);

                    // search index of exit basic block in cfg
                    for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                        if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.notTakenTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"red\" ]");
                            break;
                        }
                    }

                    // search index for all exit basic blocks in cfg
                    foreach (BasicBlock exitBasicBlock in tempBranch.takenTarget) {
                        for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                            if (methodCfg.basicBlocks.ElementAt(tempIdx) == exitBasicBlock) {
                                dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"green\" ]");
                                break;
                            }
                        }
                    }
                }
                else if (methodCfg.basicBlocks.ElementAt(idx).exitBranch as ExceptionBranchTarget != null) {
                    ExceptionBranchTarget tempBranch = (methodCfg.basicBlocks.ElementAt(idx).exitBranch as ExceptionBranchTarget);

                    // search index of exit basic block in cfg
                    bool exitTargetFound = false;
                    bool exceptionTargetFound = false;
                    for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                        if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.exitTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"green\" ]");
                            exitTargetFound = true;
                        }
                        else if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.exceptionTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"red\" ]");
                            exceptionTargetFound = true;
                        }
                        else if (exitTargetFound && exceptionTargetFound) {
                            break;
                        }
                    }
                }
            }

            // finish graph
            dotFile.WriteLine("}");

            dotFile.Close();



            // dump cfg created from the entry branches
            dotFile = new System.IO.StreamWriter(this.logPath + "\\entryBranches_" + fileName + ".dot");

            // start .dot file graph
            dotFile.WriteLine("digraph G {");

            // write all basic blocks to .dot file
            for (int idx = 0; idx < methodCfg.basicBlocks.Count(); idx++) {

                BasicBlock currentBasicBlock = methodCfg.basicBlocks.ElementAt(idx);

                // write the current basic block to the file and all its instructions
                dotFile.WriteLine("BB" + idx.ToString() + " [shape=record]");
                bool first = true;
                for (int opIdx = 0; opIdx < currentBasicBlock.operations.Count(); opIdx++) {
                    var operation = currentBasicBlock.operations.ElementAt(opIdx);
                    if (first) {
                        dotFile.Write("BB" + idx.ToString() + " [label=\"{");
                        dotFile.Write("NAME: BB" + idx.ToString() + "|");
                        dotFile.Write("ID: " + currentBasicBlock.id.ToString() + "|");
                        dotFile.Write("SEMANTIC ID: " + currentBasicBlock.semanticId.ToString() + "|");
                        first = false;

                        // dump basic block transformation metadata
                        foreach (ITransformationMetadata metadata in currentBasicBlock.transformationMetadata) {
                            if ((metadata as GraphTransformerMetadataBasicBlock) != null) {

                                GraphTransformerMetadataBasicBlock temp = (metadata as GraphTransformerMetadataBasicBlock);
                                String validPathIdString = "";
                                for (int linkIdx = 0; linkIdx < temp.correspondingGraphNodes.Count(); linkIdx++) {
                                    validPathIdString += temp.correspondingGraphNodes.ElementAt(linkIdx).validPathId.ToString() + ";";
                                }

                                dotFile.Write("GRAPH_BLOCK (valid ID: " + validPathIdString + ")|");

                                // dump position of the element in the graph (for each valid path)
                                for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                    String positionString = "";
                                    foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                        positionString += position.ToString() + ";";
                                    }
                                    dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                }

                                // dump the valid and invalid interfaces of the graph element (for each valid path)
                                for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {

                                    PathElement pathElement = null;
                                    foreach (PathElement tempElement in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.pathElements) {
                                        if (tempElement.validPathId == temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId) {
                                            pathElement = tempElement;
                                            break;
                                        }
                                    }
                                    if (pathElement == null) {
                                        throw new ArgumentNullException("No path element for valid graph id was found.");
                                    }


                                    String tempOutput = "";
                                    foreach (ITypeReference interfaceInList in pathElement.validInterfaces) {
                                        tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                                    }
                                    dotFile.Write("valid Interfaces: " + tempOutput + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");

                                    tempOutput = "";
                                    foreach (ITypeReference interfaceInList in pathElement.invalidInterfaces) {
                                        tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                                    }
                                    dotFile.Write("invalid Interfaces: " + tempOutput + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                }
                            }
                            else if ((metadata as GraphTransformerPredicateBasicBlock) != null) {

                                GraphTransformerPredicateBasicBlock temp = (metadata as GraphTransformerPredicateBasicBlock);
                                String validPathIdString = "";
                                for (int linkIdx = 0; linkIdx < temp.correspondingGraphNodes.Count(); linkIdx++) {
                                    validPathIdString += temp.correspondingGraphNodes.ElementAt(linkIdx).validPathId.ToString() + ";";
                                }

                                if (temp.predicateType == GraphOpaquePredicate.True) {
                                    dotFile.Write("TRUE_PREDICATE_BLOCK (valid ID: " + validPathIdString + ")|");

                                    // dump position of the element in the graph (for each valid path)
                                    for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                        String positionString = "";
                                        foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                            positionString += position.ToString() + ";";
                                        }
                                        dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                    }
                                }
                                else if (temp.predicateType == GraphOpaquePredicate.False) {
                                    dotFile.Write("FALSE_PREDICATE_BLOCK (valid ID: " + validPathIdString + ")|");

                                    // dump position of the element in the graph (for each valid path)
                                    for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                        String positionString = "";
                                        foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                            positionString += position.ToString() + ";";
                                        }
                                        dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                    }
                                }
                                else if (temp.predicateType == GraphOpaquePredicate.Random) {
                                    dotFile.Write("RANDOM_PREDICATE_BLOCK (valid ID: " + validPathIdString + ")|");

                                    // dump position of the element in the graph (for each valid path)
                                    for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                        String positionString = "";
                                        foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                            positionString += position.ToString() + ";";
                                        }
                                        dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                    }
                                }
                                else {
                                    throw new ArgumentException("Do not know how to handle opaque predicate type.");
                                }
                            }
                            else if ((metadata as GraphTransformerNextNodeBasicBlock) != null) {

                                GraphTransformerNextNodeBasicBlock temp = (metadata as GraphTransformerNextNodeBasicBlock);
                                String validPathIdString = "";
                                for (int linkIdx = 0; linkIdx < temp.correspondingGraphNodes.Count(); linkIdx++) {
                                    validPathIdString += temp.correspondingGraphNodes.ElementAt(linkIdx).validPathId.ToString() + ";";
                                }

                                if (temp.correctNextNode) {
                                    dotFile.Write("CORRECT_NEXT_NODE_BLOCK (valid ID: " + validPathIdString + ")|");

                                    // dump position of the element in the graph (for each valid path)
                                    for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                        String positionString = "";
                                        foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                            positionString += position.ToString() + ";";
                                        }
                                        dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                    }
                                }
                                else {
                                    dotFile.Write("WRONG_NEXT_NODE_BLOCK (valid ID: " + validPathIdString + ")|");

                                    // dump position of the element in the graph (for each valid path)
                                    for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                        String positionString = "";
                                        foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                            positionString += position.ToString() + ";";
                                        }
                                        dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                    }
                                }
                            }
                            else if ((metadata as GraphTransformerDeadCodeBasicBlock) != null) {

                                GraphTransformerDeadCodeBasicBlock temp = (metadata as GraphTransformerDeadCodeBasicBlock);

                                dotFile.Write("DEAD_CODE_BLOCK (template semantic ID: " + temp.semanticId.ToString() + ")|");
                            }
                            else if ((metadata as GraphTransformerStateBasicBlock) != null) {

                                GraphTransformerStateBasicBlock temp = (metadata as GraphTransformerStateBasicBlock);
                                String validPathIdString = "";
                                for (int linkIdx = 0; linkIdx < temp.correspondingGraphNodes.Count(); linkIdx++) {
                                    validPathIdString += temp.correspondingGraphNodes.ElementAt(linkIdx).validPathId.ToString() + ";";
                                }

                                dotFile.Write("STATE_BLOCK (valid ID: " + validPathIdString + ")|");

                                // dump position of the element in the graph (for each valid path)
                                for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                    String positionString = "";
                                    foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                        positionString += position.ToString() + ";";
                                    }
                                    dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                }
                            }
                            else if ((metadata as GraphTransformerStateChangeBasicBlock) != null) {

                                GraphTransformerStateChangeBasicBlock temp = (metadata as GraphTransformerStateChangeBasicBlock);
                                String validPathIdString = "";
                                for (int linkIdx = 0; linkIdx < temp.correspondingGraphNodes.Count(); linkIdx++) {
                                    validPathIdString += temp.correspondingGraphNodes.ElementAt(linkIdx).validPathId.ToString() + ";";
                                }

                                dotFile.Write("STATE_CHANGE_BLOCK (valid ID: " + validPathIdString + ")|");

                                // dump position of the element in the graph (for each valid path)
                                for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                    String positionString = "";
                                    foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                        positionString += position.ToString() + ";";
                                    }
                                    dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                }
                            }
                            else if ((metadata as GraphTransformerIntermediateBasicBlock) != null) {

                                GraphTransformerIntermediateBasicBlock temp = (metadata as GraphTransformerIntermediateBasicBlock);
                                String validPathIdString = "";
                                for (int linkIdx = 0; linkIdx < temp.correspondingGraphNodes.Count(); linkIdx++) {
                                    validPathIdString += temp.correspondingGraphNodes.ElementAt(linkIdx).validPathId.ToString() + ";";
                                }

                                dotFile.Write("INTERMEDIATE_BASIC_BLOCK (valid ID: " + validPathIdString + ")|");

                                // dump position of the element in the graph (for each valid path)
                                for (int nodeIdx = 0; nodeIdx < temp.correspondingGraphNodes.Count(); nodeIdx++) {
                                    String positionString = "";
                                    foreach (int position in temp.correspondingGraphNodes.ElementAt(nodeIdx).graphNode.positionInGraph) {
                                        positionString += position.ToString() + ";";
                                    }
                                    dotFile.Write("POSITION " + positionString + " (valid ID: " + temp.correspondingGraphNodes.ElementAt(nodeIdx).validPathId.ToString() + ")|");
                                }

                            }
                            else if ((metadata as DevBasicBlock) != null) {

                                DevBasicBlock temp = (metadata as DevBasicBlock);

                                dotFile.Write("DEV_NOTE: " + temp.note + "|");
                            }
                            else {
                                throw new ArgumentException("Do not know how to handle transformation metadata.");
                            }
                        }

                    }
                    else {
                        dotFile.Write("|");
                    }

                    // insert try block beginnings
                    foreach (var tryBlock in currentBasicBlock.tryBlocks) {
                        if (tryBlock.firstBasicBlockOfTryBlock && opIdx == 0) {
                            dotFile.Write("TRY START (" + this.sanitizeString(tryBlock.exceptionHandler.ExceptionType.ToString()) + ")|");
                        }
                    }

                    // insert catch block beginnings
                    foreach (var handlerBlock in currentBasicBlock.handlerBlocks) {
                        if (handlerBlock.firstBasicBlockOfHandlerBlock && opIdx == 0) {
                            if (handlerBlock.typeOfHandler == HandlerKind.Catch) {
                                dotFile.Write("CATCH START (" + this.sanitizeString(handlerBlock.exceptionHandler.ExceptionType.ToString()) + ")|");
                            }
                            else if (handlerBlock.typeOfHandler == HandlerKind.Finally) {
                                dotFile.Write("FINALLY START (" + this.sanitizeString(handlerBlock.exceptionHandler.ExceptionType.ToString()) + ")|");
                            }
                            else {
                                throw new ArgumentException("Do not know how to handle handler.");
                            }
                        }
                    }

                    // check if instruction has an argument
                    if (operation.Value != null) {
                        dotFile.Write(operation.OperationCode.ToString() + " " + this.sanitizeString(operation.Value.ToString()));
                    }
                    else {
                        dotFile.Write(operation.OperationCode.ToString());
                    }

                    // insert try block endings
                    foreach (var tryBlock in currentBasicBlock.tryBlocks) {
                        if (tryBlock.lastBasicBlockOfTryBlock && (currentBasicBlock.operations.Count() - 1) == opIdx) {
                            dotFile.Write("|TRY END (" + this.sanitizeString(tryBlock.exceptionHandler.ExceptionType.ToString()) + ")");
                        }
                    }

                    // insert catch block endings
                    foreach (var handlerBlock in currentBasicBlock.handlerBlocks) {
                        if (handlerBlock.lastBasicBlockOfHandlerBlock && (currentBasicBlock.operations.Count() - 1) == opIdx) {
                            if (handlerBlock.typeOfHandler == HandlerKind.Catch) {
                                dotFile.Write("|CATCH END (" + this.sanitizeString(handlerBlock.exceptionHandler.ExceptionType.ToString()) + ")");
                            }
                            else if (handlerBlock.typeOfHandler == HandlerKind.Finally) {
                                dotFile.Write("|FINALLY END (" + this.sanitizeString(handlerBlock.exceptionHandler.ExceptionType.ToString()) + ")");
                            }
                            else {
                                throw new ArgumentException("Do not know how to handle handler.");
                            }
                        }
                    }
                }
                dotFile.WriteLine("}\"]");

                foreach (IBranchTarget entry in methodCfg.basicBlocks.ElementAt(idx).entryBranches) {

                    // search index of exit basic block in cfg
                    for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                        if (methodCfg.basicBlocks.ElementAt(tempIdx) == entry.sourceBasicBlock) {
                            dotFile.WriteLine("BB" + tempIdx.ToString() + " -> BB" + idx.ToString() + "[ color=\"blue\" ]");
                            break;
                        }
                    }
                }
            }

            // finish graph
            dotFile.WriteLine("}");

            dotFile.Close();


            // dump a clean cfg created from the exit branches
            dotFile = new System.IO.StreamWriter(this.logPath + "\\clean_" + fileName + ".dot");

            // start .dot file graph
            dotFile.WriteLine("digraph G {");

            // write all basic blocks to .dot file
            for (int idx = 0; idx < methodCfg.basicBlocks.Count(); idx++) {

                BasicBlock currentBasicBlock = methodCfg.basicBlocks.ElementAt(idx);

                // write the current basic block to the file
                dotFile.WriteLine("BB" + idx.ToString() + " [shape=record]");
                dotFile.WriteLine("BB" + idx.ToString() + " [label=\"\"]");

                // write all the exits of the basic block to the file
                if (methodCfg.basicBlocks.ElementAt(idx).exitBranch as NoBranchTarget != null) {
                    NoBranchTarget tempBranch = (methodCfg.basicBlocks.ElementAt(idx).exitBranch as NoBranchTarget);

                    // search index of exit basic block in cfg
                    for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                        if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.takenTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"black\" ]");
                            break;
                        }
                    }
                }
                else if (methodCfg.basicBlocks.ElementAt(idx).exitBranch as TryBlockTarget != null) {
                    TryBlockTarget tempBranch = (methodCfg.basicBlocks.ElementAt(idx).exitBranch as TryBlockTarget);

                    // search index of exit basic block in cfg
                    for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                        if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.takenTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"black\" ]");
                            break;
                        }
                    }
                }
                else if (methodCfg.basicBlocks.ElementAt(idx).exitBranch as UnconditionalBranchTarget != null) {
                    UnconditionalBranchTarget tempBranch = (methodCfg.basicBlocks.ElementAt(idx).exitBranch as UnconditionalBranchTarget);

                    // search index of exit basic block in cfg
                    for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                        if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.takenTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"black\" ]");
                            break;
                        }
                    }
                }
                else if (methodCfg.basicBlocks.ElementAt(idx).exitBranch as ConditionalBranchTarget != null) {
                    ConditionalBranchTarget tempBranch = (methodCfg.basicBlocks.ElementAt(idx).exitBranch as ConditionalBranchTarget);

                    // search index of exit basic block in cfg
                    bool takenTargetFound = false;
                    bool notTakenTargetFound = false;
                    for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                        if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.takenTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"black\" ]");
                            takenTargetFound = true;
                        }
                        else if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.notTakenTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"black\" ]");
                            notTakenTargetFound = true;
                        }
                        else if (takenTargetFound && notTakenTargetFound) {
                            break;
                        }
                    }
                }
                else if (methodCfg.basicBlocks.ElementAt(idx).exitBranch as SwitchBranchTarget != null) {
                    SwitchBranchTarget tempBranch = (methodCfg.basicBlocks.ElementAt(idx).exitBranch as SwitchBranchTarget);

                    // search index of exit basic block in cfg
                    for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                        if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.notTakenTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"black\" ]");
                            break;
                        }
                    }

                    // search index for all exit basic blocks in cfg
                    foreach (BasicBlock exitBasicBlock in tempBranch.takenTarget) {
                        for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                            if (methodCfg.basicBlocks.ElementAt(tempIdx) == exitBasicBlock) {
                                dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"black\" ]");
                                break;
                            }
                        }
                    }
                }
                else if (methodCfg.basicBlocks.ElementAt(idx).exitBranch as ExceptionBranchTarget != null) {
                    ExceptionBranchTarget tempBranch = (methodCfg.basicBlocks.ElementAt(idx).exitBranch as ExceptionBranchTarget);

                    // search index of exit basic block in cfg
                    bool exitTargetFound = false;
                    bool exceptionTargetFound = false;
                    for (int tempIdx = 0; tempIdx < methodCfg.basicBlocks.Count(); tempIdx++) {
                        if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.exitTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"black\" ]");
                            exitTargetFound = true;
                        }
                        else if (methodCfg.basicBlocks.ElementAt(tempIdx) == tempBranch.exceptionTarget) {
                            dotFile.WriteLine("BB" + idx.ToString() + " -> BB" + tempIdx.ToString() + "[ color=\"black\" ]");
                            exceptionTargetFound = true;
                        }
                        else if (exitTargetFound && exceptionTargetFound) {
                            break;
                        }
                    }
                }
            }

            // finish graph
            dotFile.WriteLine("}");

            dotFile.Close();

        }


        // dumps the rest of the graph recursively
        // IMPORTANT: will crash if a loop exists in the graph
        private void dumpGraphRecursively(System.IO.StreamWriter dotFile, NodeObject currentNode, String nodeName, bool dumpOnlyValidPath) {

            // write current node to file but only the valid path
            if (dumpOnlyValidPath) {

                if (currentNode.elementOfValidPath.Count() == 0) {
                    return;
                }

                dotFile.WriteLine(nodeName + " [shape=record]");

                dotFile.Write(nodeName + " [label=\"{");
                dotFile.Write("Node: " + nodeName + "|");
                dotFile.Write("Class: " + this.sanitizeString(currentNode.thisClass.ToString()) + "|");

                String tempOutput = "";
                foreach (PathElement pathElement in currentNode.pathElements) {

                    tempOutput = "";
                    foreach (ITypeReference interfaceInList in pathElement.validInterfaces) {
                        tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                    }
                    dotFile.Write("valid Interfaces (valid ID: " + pathElement.validPathId.ToString() + "): " + tempOutput + "|");

                    tempOutput = "";
                    foreach (ITypeReference interfaceInList in pathElement.invalidInterfaces) {
                        tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                    }
                    dotFile.Write("invalid Interfaces (valid ID: " + pathElement.validPathId.ToString() + "): " + tempOutput + "|");

                }

                dotFile.Write("Constructor: " + this.makeFuncSigString(currentNode.constructorToUse) + "|");

                tempOutput = "";
                foreach (ITypeReference interfaceInList in currentNode.pathElements.ElementAt(0).mandatoryInterfaces) {
                    tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                }
                dotFile.Write("mandatory Interfaces: " + tempOutput + "|");

                tempOutput = "";
                foreach (ITypeReference interfaceInList in currentNode.pathElements.ElementAt(0).forbiddenInterfaces) {
                    tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                }
                dotFile.Write("forbidden Interfaces: " + tempOutput + "|");

                tempOutput = "";
                foreach (NamespaceTypeDefinition classInList in currentNode.possibleClasses) {
                    tempOutput += this.sanitizeString(classInList.ToString()) + "; ";
                }
                dotFile.Write("possible Classes: " + tempOutput + "|");

                String positionString = "";
                foreach (int position in currentNode.positionInGraph) {
                    positionString += position.ToString() + ";";
                }
                dotFile.Write("Position: " + positionString + "|");

                positionString = "";
                foreach (NodeObject possibleNode in currentNode.possibleExchangeObjects) {
                    positionString += "(";
                    foreach (int position in possibleNode.positionInGraph) {
                        positionString += position.ToString() + ";";
                    }
                    positionString += ") ";
                }
                dotFile.Write("Possible exchange nodes: " + positionString + "|");

                String validPathIdString = "";
                foreach (int validPathId in currentNode.elementOfValidPath) {
                    validPathIdString += validPathId.ToString() + ";";
                }
                dotFile.Write("Valid Path Ids: " + validPathIdString);

                dotFile.WriteLine("}\"]");

                for (int idx = 0; idx < currentNode.dimension; idx++) {

                    // check if the tree is a complete tree
                    if (currentNode.nodeObjects[idx] == null) {
                        continue;
                    }

                    if (currentNode.nodeObjects[idx].elementOfValidPath.Count() != 0) {
                        dotFile.WriteLine(nodeName + "-> " + nodeName + "_" + idx.ToString() + "[ color=\"green\" ]");

                        // dump rest of the graph recursively
                        this.dumpGraphRecursively(dotFile, currentNode.nodeObjects[idx], nodeName + "_" + idx.ToString(), true);
                    }
                }
            }

            // write current node to file with the whole graph
            else {

                dotFile.WriteLine(nodeName + " [shape=record]");

                dotFile.Write(nodeName + " [label=\"{");
                dotFile.Write("Node: " + nodeName + "|");
                dotFile.Write("Class: " + this.sanitizeString(currentNode.thisClass.ToString()) + "|");

                String tempOutput = "";
                foreach (PathElement pathElement in currentNode.pathElements) {

                    tempOutput = "";
                    foreach (ITypeReference interfaceInList in pathElement.validInterfaces) {
                        tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                    }
                    dotFile.Write("valid Interfaces (valid ID: " + pathElement.validPathId.ToString() + "): " + tempOutput + "|");

                    tempOutput = "";
                    foreach (ITypeReference interfaceInList in pathElement.invalidInterfaces) {
                        tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                    }
                    dotFile.Write("invalid Interfaces (valid ID: " + pathElement.validPathId.ToString() + "): " + tempOutput + "|");

                }

                dotFile.Write("Constructor: " + this.makeFuncSigString(currentNode.constructorToUse) + "|");

                tempOutput = "";
                foreach (ITypeReference interfaceInList in currentNode.pathElements.ElementAt(0).mandatoryInterfaces) {
                    tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                }
                dotFile.Write("mandatory Interfaces: " + tempOutput + "|");

                tempOutput = "";
                foreach (ITypeReference interfaceInList in currentNode.pathElements.ElementAt(0).forbiddenInterfaces) {
                    tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                }
                dotFile.Write("forbidden Interfaces: " + tempOutput + "|");

                tempOutput = "";
                foreach (NamespaceTypeDefinition classInList in currentNode.possibleClasses) {
                    tempOutput += this.sanitizeString(classInList.ToString()) + "; ";
                }
                dotFile.Write("possible Classes: " + tempOutput + "|");

                String positionString = "";
                foreach (int position in currentNode.positionInGraph) {
                    positionString += position.ToString() + ";";
                }
                dotFile.Write("Position: " + positionString + "|");

                positionString = "";
                foreach (NodeObject possibleNode in currentNode.possibleExchangeObjects) {
                    positionString += "(";
                    foreach (int position in possibleNode.positionInGraph) {
                        positionString += position.ToString() + ";";
                    }
                    positionString += ") ";
                }
                dotFile.Write("Possible exchange nodes: " + positionString + "|");

                String validPathIdString = "";
                foreach (int validPathId in currentNode.elementOfValidPath) {
                    validPathIdString += validPathId.ToString() + ";";
                }
                dotFile.Write("Valid Path Ids: " + validPathIdString);

                dotFile.WriteLine("}\"]");

                for (int idx = 0; idx < currentNode.dimension; idx++) {

                    // check if the tree is a complete tree
                    if (currentNode.nodeObjects[idx] == null) {
                        continue;
                    }

                    if (currentNode.nodeObjects[idx].elementOfValidPath.Count() != 0) {
                        dotFile.WriteLine(nodeName + "-> " + nodeName + "_" + idx.ToString() + "[ color=\"green\" ]");
                    }
                    else {
                        if (!dumpOnlyValidPath) {
                            dotFile.WriteLine(nodeName + "-> " + nodeName + "_" + idx.ToString() + "[ color=\"red\" ]");
                        }
                    }

                    // dump rest of the graph recursively
                    this.dumpGraphRecursively(dotFile, currentNode.nodeObjects[idx], nodeName + "_" + idx.ToString(), false);
                }
            }
        }


        // dump graph to a .dot file
        // IMPORTANT: will crash if a loop exists in the graph
        public void dumpGraph(NodeObject startNode, String fileName, bool dumpOnlyValidPath) {

            // shorten file name if it is too long
            fileName = fileName.Length >= 230 ? fileName.Substring(0, 230) : fileName;

            // dump graph
            System.IO.StreamWriter dotFile = new System.IO.StreamWriter(this.logPath + "\\graph_" + fileName + ".dot");

            // start .dot file graph
            dotFile.WriteLine("digraph G {");

            // write start node to file but only the valid path
            String nodeName = "Node_0";
            if (dumpOnlyValidPath) {

                if (startNode.elementOfValidPath.Count() != 0) {
                    dotFile.WriteLine(nodeName + " [shape=record]");

                    dotFile.Write(nodeName + " [label=\"{");
                    dotFile.Write("Node: " + nodeName + "|");
                    dotFile.Write("Class: " + this.sanitizeString(startNode.thisClass.ToString()) + "|");

                    String tempOutput = "";
                    foreach(PathElement pathElement in startNode.pathElements) {

                        tempOutput = "";
                        foreach (ITypeReference interfaceInList in pathElement.validInterfaces) {
                            tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                        }
                        dotFile.Write("valid Interfaces (valid ID: " + pathElement.validPathId.ToString() + "): " + tempOutput + "|");

                        tempOutput = "";
                        foreach (ITypeReference interfaceInList in pathElement.invalidInterfaces) {
                            tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                        }
                        dotFile.Write("invalid Interfaces (valid ID: " + pathElement.validPathId.ToString() + "): " + tempOutput + "|");

                    }

                    dotFile.Write("Constructor: " + this.makeFuncSigString(startNode.constructorToUse) + "|");

                    tempOutput = "";
                    foreach (ITypeReference interfaceInList in startNode.pathElements.ElementAt(0).mandatoryInterfaces) {
                        tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                    }
                    dotFile.Write("mandatory Interfaces: " + tempOutput + "|");

                    tempOutput = "";
                    foreach (ITypeReference interfaceInList in startNode.pathElements.ElementAt(0).forbiddenInterfaces) {
                        tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                    }
                    dotFile.Write("forbidden Interfaces: " + tempOutput + "|");

                    tempOutput = "";
                    foreach (NamespaceTypeDefinition classInList in startNode.possibleClasses) {
                        tempOutput += this.sanitizeString(classInList.ToString()) + "; ";
                    }
                    dotFile.Write("possible Classes: " + tempOutput + "|");

                    String positionString = "";
                    foreach (int position in startNode.positionInGraph) {
                        positionString += position.ToString() + ";";
                    }
                    dotFile.Write("Position: " + positionString + "|");

                    positionString = "";
                    foreach (NodeObject possibleNode in startNode.possibleExchangeObjects) {
                        positionString += "(";
                        foreach (int position in possibleNode.positionInGraph) {
                            positionString += position.ToString() + ";";
                        }
                        positionString += ") ";
                    }
                    dotFile.Write("Possible exchange nodes: " + positionString + "|");

                    String validPathIdString = "";
                    foreach (int validPathId in startNode.elementOfValidPath) {
                        validPathIdString += validPathId.ToString() + ";";
                    }
                    dotFile.Write("Valid Path Ids: " + validPathIdString);

                    dotFile.WriteLine("}\"]");

                    for (int idx = 0; idx < startNode.dimension; idx++) {

                        // check if the tree is a complete tree
                        if (startNode.nodeObjects[idx] == null) {
                            continue;
                        }

                        if (startNode.nodeObjects[idx].elementOfValidPath.Count() != 0) {
                            dotFile.WriteLine(nodeName + "-> " + nodeName + "_" + idx.ToString() + "[ color=\"green\" ]");

                            // dump rest of the graph recursively
                            this.dumpGraphRecursively(dotFile, startNode.nodeObjects[idx], nodeName + "_" + idx.ToString(), true);
                        }
                    }
                }
            }

            // write start node to file with the whole graph
            else {
                dotFile.WriteLine(nodeName + " [shape=record]");

                dotFile.Write(nodeName + " [label=\"{");
                dotFile.Write("Node: " + nodeName + "|");
                dotFile.Write("Class: " + this.sanitizeString(startNode.thisClass.ToString()) + "|");

                String tempOutput = "";
                foreach (PathElement pathElement in startNode.pathElements) {

                    tempOutput = "";
                    foreach (ITypeReference interfaceInList in pathElement.validInterfaces) {
                        tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                    }
                    dotFile.Write("valid Interfaces (valid ID: " + pathElement.validPathId.ToString() + "): " + tempOutput + "|");

                    tempOutput = "";
                    foreach (ITypeReference interfaceInList in pathElement.invalidInterfaces) {
                        tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                    }
                    dotFile.Write("invalid Interfaces (valid ID: " + pathElement.validPathId.ToString() + "): " + tempOutput + "|");

                }

                dotFile.Write("Constructor: " + this.makeFuncSigString(startNode.constructorToUse) + "|");

                tempOutput = "";
                foreach (ITypeReference interfaceInList in startNode.pathElements.ElementAt(0).mandatoryInterfaces) {
                    tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                }
                dotFile.Write("mandatory Interfaces: " + tempOutput + "|");

                tempOutput = "";
                foreach (ITypeReference interfaceInList in startNode.pathElements.ElementAt(0).forbiddenInterfaces) {
                    tempOutput += this.sanitizeString(interfaceInList.ToString()) + "; ";
                }
                dotFile.Write("forbidden Interfaces: " + tempOutput + "|");

                tempOutput = "";
                foreach (NamespaceTypeDefinition classInList in startNode.possibleClasses) {
                    tempOutput += this.sanitizeString(classInList.ToString()) + "; ";
                }
                dotFile.Write("possible Classes: " + tempOutput + "|");

                String positionString = "";
                foreach (int position in startNode.positionInGraph) {
                    positionString += position.ToString() + ";";
                }
                dotFile.Write("Position: " + positionString + "|");

                positionString = "";
                foreach (NodeObject possibleNode in startNode.possibleExchangeObjects) {
                    positionString += "(";
                    foreach (int position in possibleNode.positionInGraph) {
                        positionString += position.ToString() + ";";
                    }
                    positionString += ") ";
                }
                dotFile.Write("Possible exchange nodes: " + positionString + "|");

                String validPathIdString = "";
                foreach (int validPathId in startNode.elementOfValidPath) {
                    validPathIdString += validPathId.ToString() + ";";
                }
                dotFile.Write("Valid Path Ids: " + validPathIdString);

                dotFile.WriteLine("}\"]");

                for (int idx = 0; idx < startNode.dimension; idx++) {

                    // check if the tree is a complete tree
                    if (startNode.nodeObjects[idx] == null) {
                        continue;
                    }

                    if (startNode.nodeObjects[idx].elementOfValidPath.Count() != 0) {
                        dotFile.WriteLine(nodeName + "-> " + nodeName + "_" + idx.ToString() + "[ color=\"green\" ]");
                    }
                    else {
                        dotFile.WriteLine(nodeName + "-> " + nodeName + "_" + idx.ToString() + "[ color=\"red\" ]");
                    }

                    // dump rest of the graph recursively
                    this.dumpGraphRecursively(dotFile, startNode.nodeObjects[idx], nodeName + "_" + idx.ToString(), false);

                }
            }






            // finish graph
            dotFile.WriteLine("}");

            dotFile.Close();

        }

    }

}
