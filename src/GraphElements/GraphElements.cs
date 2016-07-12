using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Cci;
using Microsoft.Cci.MutableCodeModel;

namespace GraphElements {

    public class NodeObject {

        public int dimension;        
        public NodeObject[] nodeObjects = null;

        public List<NamespaceTypeDefinition> possibleClasses = null;
        public List<NodeObject> possibleExchangeObjects = null;

        public NamespaceTypeDefinition thisClass;
        public MethodDefinition constructorToUse = null;
        public List<PathElement> pathElements = new List<PathElement>();

        public int validPathCount;
        public List<int> elementOfValidPath = null;

        public List<int> positionInGraph = null;


        public NodeObject(int dimension, int validPathCount, NamespaceTypeDefinition thisClass, MethodDefinition constructorToUse, PathElement pathElement, List<int> positionInGraph) {

            // a list of possible objects in the graph that fulfill the needed attributes like this object
            this.possibleExchangeObjects = new List<NodeObject>();

            // a list of possible classes this object could made of
            this.possibleClasses = new List<NamespaceTypeDefinition>();

            // set the amount of child nodes this node object has
            this.dimension = dimension;
            this.nodeObjects = new NodeObject[this.dimension];

            // class to use for this node object
            this.thisClass = thisClass;

            // constructor to use when a node for the graph is created
            this.constructorToUse = constructorToUse;

            // interfaces that this object represents (or not represents)
            this.pathElements.Add(pathElement);

            // set the maximum amount of valid paths that this node can be an element of
            // (in the list elementOfValidPath is the id of the valid path stored this element is a part of)
            this.validPathCount = validPathCount;
            this.elementOfValidPath = new List<int>(this.validPathCount);

            // set the position of this node in the graph
            this.positionInGraph = positionInGraph;

            // null every object in the array
            for (int i = 0; i < this.dimension; i++) {
                this.nodeObjects[i] = null;
            }
        }


        public NodeObject(int dimension, int validPathCount, NamespaceTypeDefinition thisClass, MethodDefinition constructorToUse, PathElement pathElement, List<int> positionInGraph, int validPathId)
            : this(dimension, validPathCount, thisClass, constructorToUse, pathElement, positionInGraph) {

            // add the id of the valid path to the list
            this.elementOfValidPath.Add(validPathId);

        }
    }


    public class PathElement {

        public int validPathId = -1;

        // these lists contain all the interfaces that are valid and invalid for this graph element
        // (used for example by the opaque predicate creation)
        public List<ITypeReference> validInterfaces = new List<ITypeReference>();
        public List<ITypeReference> invalidInterfaces = new List<ITypeReference>();

        // these lists contain all the interfaces that are mandatory and forbidden for this graph element
        // (used for example for the graph creation, is not necessary the same as valid/invalid list because
        // these lists also consider interfaces valid/invalid for other graph elements that are at the same position)
        public List<ITypeReference> mandatoryInterfaces = new List<ITypeReference>();
        public List<ITypeReference> forbiddenInterfaces = new List<ITypeReference>();

        // represents the link to the graph object this path element belongs to
        public NodeObject linkGraphObject = null;
    }


    public class ValidGraphPath {

        public PathElement[] pathElements;
        public int[] pathIndices;

        public ValidGraphPath(int length) {

            this.pathElements = new PathElement[length];
            this.pathIndices = new int[length];

        }

    }


    public struct PossibleNode {
        public NamespaceTypeDefinition givenClass;
        public MethodDefinition nodeConstructor;

    }


}
