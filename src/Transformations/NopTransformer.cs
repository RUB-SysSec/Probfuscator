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

namespace Transformations {

    // simple test transformer that adds a NOP instruction after every instruction in the method
    public class NopTransformer {

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


        public NopTransformer(IModule module, PeReader.DefaultHost host, Log.Log logger) {
            this.host = host;
            this.logger = logger;
            this.module = module;
        }


        // adds a nop instruction after every instruction in the given method cfg
        public void addNopsToCfg(MethodCfg methodCfg) {

            foreach (BasicBlock tempBB in methodCfg.basicBlocks) {

                List<IOperation> newOperations = new List<IOperation>();
                for (int idx = 0; idx < tempBB.operations.Count(); idx++) {

                    IOperation currentOperation = tempBB.operations.ElementAt(idx);

                    // copy current operation to new operations list
                    newOperations.Add(currentOperation);

                    if (idx == tempBB.operations.Count() - 1) {
                        break;
                    }

                    // ignore all prefixes (because nop after prefix will crash the program)
                    if (currentOperation.OperationCode == OperationCode.No_
                        || currentOperation.OperationCode == OperationCode.Constrained_
                        || currentOperation.OperationCode == OperationCode.Readonly_
                        || currentOperation.OperationCode == OperationCode.Tail_
                        || currentOperation.OperationCode == OperationCode.Unaligned_
                        || currentOperation.OperationCode == OperationCode.Volatile_) {
                        continue;
                    }

                    InternalOperation newNopOperation = new InternalOperation();
                    newNopOperation.OperationCode = OperationCode.Nop;

                    // add nop operation after current one
                    newOperations.Add(newNopOperation);

                }

                // replace old operations with new ones
                tempBB.operations = newOperations;

            }

        }

    }

}
