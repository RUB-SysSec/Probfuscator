using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GraphElements;

namespace TransformationsMetadata {

    public enum GraphOpaquePredicate { True, False, Random };


    public interface ITransformationMetadata {
    }


    // this class is used to link the basic block metadata to the according graph node
    public class BasicBlockGraphNodeLink {
        public NodeObject graphNode;
        public int validPathId;

        public BasicBlockGraphNodeLink(NodeObject graphNode, int validPathId) {
            this.validPathId = validPathId;
            this.graphNode = graphNode;
        }
    }


    // interface that implements information that is needed by every graph transformation metadata class
    public interface IGraphTransformerMetadata {
        List<BasicBlockGraphNodeLink> correspondingGraphNodes {set; get;}
    }


    // this class contains all metadata of the graph transformation for the basic block
    public class GraphTransformerMetadataBasicBlock : IGraphTransformerMetadata, ITransformationMetadata {

        // TODO id of graph?

        public List<BasicBlockGraphNodeLink> correspondingGraphNodes {set; get;}
        
        public GraphTransformerMetadataBasicBlock() {
            this.correspondingGraphNodes = new List<BasicBlockGraphNodeLink>();
        }

    }


    // this class contains metadata of the basic block if it is an opaque predicate of the graph
    public class GraphTransformerPredicateBasicBlock : IGraphTransformerMetadata, ITransformationMetadata {

        // TODO id of graph?

        public GraphOpaquePredicate predicateType;

        public List<BasicBlockGraphNodeLink> correspondingGraphNodes {set; get;}

        public GraphTransformerPredicateBasicBlock() {
            this.correspondingGraphNodes = new List<BasicBlockGraphNodeLink>();
        }

    }


    // this class contains metadata of the basic block if it is code to get the next node in the graph
    public class GraphTransformerNextNodeBasicBlock : IGraphTransformerMetadata, ITransformationMetadata {

        // TODO id of graph?

        public bool correctNextNode;

        public List<BasicBlockGraphNodeLink> correspondingGraphNodes {set; get;}

        public GraphTransformerNextNodeBasicBlock() {
            this.correspondingGraphNodes = new List<BasicBlockGraphNodeLink>();
        }

    }


    // this class contains metadata of the basic block if it is dead code of a graph node
    public class GraphTransformerDeadCodeBasicBlock : IGraphTransformerMetadata, ITransformationMetadata {

        // TODO id of graph?

        // the semantic id of the basic block that should be used as template for the dead code generation
        // (if set to -1 there is no template)
        public int semanticId = -1;

        public List<BasicBlockGraphNodeLink> correspondingGraphNodes { set; get; }

        public GraphTransformerDeadCodeBasicBlock() {
            this.correspondingGraphNodes = new List<BasicBlockGraphNodeLink>();
        }

    }


    // this class contains metadata of the basic block if it is a state branch
    public class GraphTransformerStateBasicBlock : IGraphTransformerMetadata, ITransformationMetadata {

        // TODO id of graph?

        public List<BasicBlockGraphNodeLink> correspondingGraphNodes { set; get; }

        public GraphTransformerStateBasicBlock() {
            this.correspondingGraphNodes = new List<BasicBlockGraphNodeLink>();
        }

    }


    // this class contains metadata of the basic block if it is a state branch
    public class GraphTransformerStateChangeBasicBlock : IGraphTransformerMetadata, ITransformationMetadata {

        // TODO id of graph?

        public List<BasicBlockGraphNodeLink> correspondingGraphNodes { set; get; }

        public GraphTransformerStateChangeBasicBlock() {
            this.correspondingGraphNodes = new List<BasicBlockGraphNodeLink>();
        }

    }


    // this class contains metadata of the basic block if it is an artificial intermediate basic block
    // which has no semantical reason to be there
    public class GraphTransformerIntermediateBasicBlock : IGraphTransformerMetadata, ITransformationMetadata {

        // TODO id of graph?

        public List<BasicBlockGraphNodeLink> correspondingGraphNodes { set; get; }

        public GraphTransformerIntermediateBasicBlock() {
            this.correspondingGraphNodes = new List<BasicBlockGraphNodeLink>();
        }

    }


    // this class contains metadata for developers for example to mark a basic block in the .dot file dump
    public class DevBasicBlock : ITransformationMetadata {

        public String note = "";

    }




}
