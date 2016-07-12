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


namespace Graph {

    public class Graph {

        public NodeObject startingNode = null;
        public int graphDepth;
        public int graphDimension;
        public int graphValidPathCount;

        // Hashtable of Lists of classes
        // Key: (ITypeReference) interface
        // Value: (List<struct possibleNode>) classes that implement this interface
        public Hashtable graphInterfaces;

        Random prng = null;


        public Graph(Hashtable graphInterfaces, Random prng, int depth, int dimension) {

            this.graphInterfaces = graphInterfaces;
            this.prng = prng;
            this.graphDepth = depth;
            this.graphDimension = dimension;
        }


        // this function fills the rest of the graph with random nodes
        private void fillNodesRecursively(NodeObject currentNode, List<int> currentPosition, int currentDepth, int maxDepth, List<PossibleNode> allPossibleNodes, ValidGraphPath[] validPaths) {

            // check if the maximal depth is reached
            if (currentDepth >= maxDepth) {
                return;
            }

            // fill all missing nodes of the current object with new random nodes
            for (int idx = 0; idx < currentNode.dimension; idx++) {

                // copy position list and add path index to the position 
                List<int> tempCurrentPosition = new List<int>(currentPosition);
                tempCurrentPosition.Add(idx);

                if (currentNode.nodeObjects[idx] == null) {

                    // get random possible node from list of all possible nodes
                    int randElement = this.prng.Next(allPossibleNodes.Count);
                    PossibleNode possibleNode = allPossibleNodes.ElementAt(randElement);
                    NamespaceTypeDefinition classToUse = possibleNode.givenClass;
                    MethodDefinition nodeConstructor = possibleNode.nodeConstructor;

                    // generate path element from chosen interface
                    PathElement pathElement = new PathElement();

                    // create new node object
                    currentNode.nodeObjects[idx] = new NodeObject(this.graphDimension, this.graphValidPathCount, classToUse, nodeConstructor, pathElement, tempCurrentPosition);


                    // search through every valid path if the new created filler node has the same attributes as the valid path node
                    // => add it to the list of possible nodes that can be used for an exchange between valid node and filler node
                    for (int validPathId = 0; validPathId < validPaths.Count(); validPathId++) {
                        for (int depth = 0; depth < validPaths[validPathId].pathElements.Count(); depth++) {

                            // check if the used class of the current filler node is in the list of possible classes of the valid path node
                            if (validPaths[validPathId].pathElements.ElementAt(depth).linkGraphObject.possibleClasses.Contains(classToUse)) {

                                // add current filler node to the list of possible exchange nodes
                                if (!validPaths[validPathId].pathElements.ElementAt(depth).linkGraphObject.possibleExchangeObjects.Contains(currentNode.nodeObjects[idx])) {
                                    validPaths[validPathId].pathElements.ElementAt(depth).linkGraphObject.possibleExchangeObjects.Add(currentNode.nodeObjects[idx]);
                                }
                            }
                        }
                    }

                }

                this.fillNodesRecursively(currentNode.nodeObjects[idx], tempCurrentPosition, currentDepth + 1, maxDepth, allPossibleNodes, validPaths);

            }
        }


        // builds a graph
        public void buildGraph(ValidGraphPath[] validPaths) {

            // check if at least one valid path is given
            int countValidPath = validPaths.Count();
            if (countValidPath < 1) {
                throw new ArgumentException("At least 1 valid path have to be given.");
            }
           
            // check if the path has enough elements
            int maxDepth = this.graphDepth;
            if (maxDepth <= 1) {
                throw new ArgumentException("Path has to have at least 2 elements.");
            }

            // set needed attributes
            this.graphValidPathCount = countValidPath;

            // a list of all possible nodes for all graph elements
            List<PossibleNode> allPossibleNodes = new List<PossibleNode>();

            // get a list of possible nodes that implement the first valid interface of the first node of the first valid path 
            ITypeReference tempInterface = validPaths[0].pathElements[0].mandatoryInterfaces.ElementAt(0);
            List<PossibleNode> possibleNodes = new List<PossibleNode>((List<PossibleNode>)this.graphInterfaces[tempInterface]);

            List<PossibleNode> copiedList = new List<PossibleNode>(possibleNodes);
            foreach (PossibleNode possibleNode in copiedList) {

                // check if all needed interfaces (by all valid paths) are contained by the possible node
                // if not => remove if from the list of possible nodes
                bool validNode = true;
                foreach (ITypeReference neededInterface in validPaths[0].pathElements[0].mandatoryInterfaces) {
                    if(!possibleNode.givenClass.Interfaces.Contains(neededInterface)) {
                        validNode = false;
                        break;
                    }
                }
                if(!validNode) {
                    possibleNodes.Remove(possibleNode);
                }

                // check if all forbidden interfaces (by all valid paths) are NOT contained by the possible node
                // if the possible node contains it => remove it from the list of possible nodes
                foreach (ITypeReference forbiddenInterface in validPaths[0].pathElements[0].forbiddenInterfaces) {
                    if(possibleNode.givenClass.Interfaces.Contains(forbiddenInterface)) {
                        validNode = false;
                        break;
                    }
                }
                if(!validNode) {
                    possibleNodes.Remove(possibleNode);
                }

            }

            if (possibleNodes.Count() == 0) {
                throw new ArgumentException("No class was found which satisfies the given requirements.");
            }

            // add all possible nodes for this graph element to the list of all possible nodes
            allPossibleNodes.AddRange(possibleNodes);


            // get first random node of graph            
            int randElement = this.prng.Next(possibleNodes.Count());
            NamespaceTypeDefinition classToUse = possibleNodes.ElementAt(randElement).givenClass;
            MethodDefinition nodeConstructor = possibleNodes.ElementAt(randElement).nodeConstructor;

            // create start node of graph
            List<int> startPosition = new List<int>();
            NodeObject startNode = new NodeObject(this.graphDimension, this.graphValidPathCount, classToUse, nodeConstructor, validPaths[0].pathElements[0], startPosition, 0);
            validPaths[0].pathElements[0].linkGraphObject = startNode;
            for (int validPathId = 1; validPathId < countValidPath; validPathId++) {
                startNode.elementOfValidPath.Add(validPathId);
                startNode.pathElements.Add(validPaths[validPathId].pathElements[0]);
                validPaths[validPathId].pathElements[0].linkGraphObject = startNode;
            }

            // add possible nodes to list of possible classes
            foreach (PossibleNode possibleNode in possibleNodes) {
                startNode.possibleClasses.Add(possibleNode.givenClass);
            }


            // build all valid paths
            for (int validPathId = 0; validPathId < countValidPath; validPathId++) {

                NodeObject currentNode = startNode;
                List<int> positionInGraph = new List<int>();

                // build a random valid path
                for (int idx = 1; idx < maxDepth; idx++) {

                    // get a list of possible nodes that implement the first valid interface of the first node of the first valid path 
                    tempInterface = validPaths[validPathId].pathElements[idx].mandatoryInterfaces.ElementAt(0);
                    possibleNodes = new List<PossibleNode>((List<PossibleNode>)this.graphInterfaces[tempInterface]);

                    // sort out nodes that do not satisfy all valid and invalid interface requirements of the node
                    copiedList = new List<PossibleNode>(possibleNodes);
                    foreach (PossibleNode possibleNode in copiedList) {

                        // check if all needed interfaces (by all valid paths) are contained by the possible node
                        // if not => remove if from the list of possible nodes
                        bool validNode = true;
                        foreach (ITypeReference neededInterface in validPaths[validPathId].pathElements[idx].mandatoryInterfaces) {
                            if (!possibleNode.givenClass.Interfaces.Contains(neededInterface)) {
                                validNode = false;
                                break;
                            }
                        }
                        if (!validNode) {
                            possibleNodes.Remove(possibleNode);
                        }

                        // check if all forbidden interfaces (by all valid paths) are NOT contained by the possible node
                        // if the possible node contains it => remove it from the list of possible nodes
                        foreach (ITypeReference forbiddenInterface in validPaths[validPathId].pathElements[idx].forbiddenInterfaces) {
                            if (possibleNode.givenClass.Interfaces.Contains(forbiddenInterface)) {
                                validNode = false;
                                break;
                            }
                        }
                        if (!validNode) {
                            possibleNodes.Remove(possibleNode);
                        }

                    }
                    if (possibleNodes.Count() == 0) {
                        throw new ArgumentException("No class was found which satisfies the given requirements.");
                    }


                    // add all distinct possible nodes for this graph element to the list of all possible nodes
                    foreach (PossibleNode possibleNode in possibleNodes) {
                        if (!allPossibleNodes.Contains(possibleNode)) {
                            allPossibleNodes.Add(possibleNode);
                        }
                    }


                    int pathIdx = validPaths[validPathId].pathIndices[idx];

                    if (currentNode.nodeObjects[pathIdx] != null) {

                        // check if the used class of the node satisfy both paths
                        NamespaceTypeDefinition usedClass = currentNode.nodeObjects[pathIdx].thisClass;
                        bool validNode = false; 
                        foreach (PossibleNode possibleNode in possibleNodes) {
                            if (usedClass == possibleNode.givenClass) {
                                validNode = true;
                                break;
                            }
                        }
                        if (!validNode) {
                            throw new ArgumentException("Node does not satisfy both valid paths.");
                        }


                        currentNode.nodeObjects[pathIdx].pathElements.Add(validPaths[validPathId].pathElements[idx]);
                        currentNode.nodeObjects[pathIdx].elementOfValidPath.Add(validPathId);
                        positionInGraph.Add(pathIdx);

                        validPaths[validPathId].pathElements[idx].linkGraphObject = currentNode.nodeObjects[pathIdx];

                    }

                    else {

                        // add path index to the position and copy position list
                        positionInGraph.Add(pathIdx);
                        List<int> tempPositionInGraph = new List<int>(positionInGraph);

                        // get random node for graph
                        randElement = this.prng.Next(possibleNodes.Count());
                        classToUse = possibleNodes.ElementAt(randElement).givenClass;
                        nodeConstructor = possibleNodes.ElementAt(randElement).nodeConstructor;

                        currentNode.nodeObjects[pathIdx] = new NodeObject(this.graphDimension, this.graphValidPathCount, classToUse, nodeConstructor, validPaths[validPathId].pathElements[idx], tempPositionInGraph, validPathId);
                        validPaths[validPathId].pathElements[idx].linkGraphObject = currentNode.nodeObjects[pathIdx];

                        // add possible nodes to list of possible classes
                        foreach (PossibleNode possibleNode in possibleNodes) {
                            currentNode.nodeObjects[pathIdx].possibleClasses.Add(possibleNode.givenClass);
                        }

                    }

                    currentNode = currentNode.nodeObjects[pathIdx];

                }

            }

            // fill the rest of the graph
            this.fillNodesRecursively(startNode, startPosition, 1, maxDepth, allPossibleNodes, validPaths);

            this.startingNode = startNode;

        }










        public ValidGraphPath[] generateValidPaths(int graphValidPathCount) {


            // initialize valid graph paths
            ValidGraphPath[] validPaths = new ValidGraphPath[graphValidPathCount];
            for (int graphIdx = 0; graphIdx < graphValidPathCount; graphIdx++) {
                validPaths[graphIdx] = new ValidGraphPath(this.graphDepth);
            }

            List<ITypeReference> allInterfacesList = this.graphInterfaces.Keys.OfType<ITypeReference>().ToList<ITypeReference>();
            PossibleNode[] currentRoundBasePossibleNodes = new PossibleNode[graphValidPathCount];


            // generate valid paths
            for (int idx = 0; idx < this.graphDepth; idx++) {

                // initialize path element
                for (int validPathIdx = 0; validPathIdx < graphValidPathCount; validPathIdx++) {
                    validPaths[validPathIdx].pathElements[idx] = new PathElement();
                    validPaths[validPathIdx].pathElements[idx].validPathId = validPathIdx;

                    if (idx != 0) {
                        validPaths[validPathIdx].pathIndices[idx] = this.prng.Next(this.graphDimension);
                    }
                    else {
                        // mark path index of start node as -1
                        validPaths[validPathIdx].pathIndices[0] = -1;
                    }
                }


                // check if it is the first element of the valid path
                // => always the root for each valid path
                if (idx == 0) {


                    // get random base class that is used to generate the valid path element (use it for all first elements of the valid paths)
                    ITypeReference baseInterface = allInterfacesList.ElementAt(this.prng.Next(allInterfacesList.Count()));
                    List<PossibleNode> baseList = (List<PossibleNode>)this.graphInterfaces[baseInterface];
                    PossibleNode basePossibleNode = baseList.ElementAt(this.prng.Next(baseList.Count()));


                    // for each valid path add a path element
                    for (int validPathIdx = 0; validPathIdx < graphValidPathCount; validPathIdx++) {

                        // get valid interface
                        ITypeReference firstInterface = basePossibleNode.givenClass.Interfaces.ElementAt(this.prng.Next(basePossibleNode.givenClass.Interfaces.Count()));
                        validPaths[validPathIdx].pathElements[idx].validInterfaces.Add(firstInterface);

                        // add interface to mandatory list (if it is not already in it)
                        if (!validPaths[0].pathElements[idx].mandatoryInterfaces.Contains(firstInterface)) {
                            validPaths[0].pathElements[idx].mandatoryInterfaces.Add(firstInterface);
                        }


                        // add valid interfaces to the path element
                        bool addAnotherInterface = true;
                        int addInterfaceWeight = 3;
                        while (addAnotherInterface) {

                            // decide wether to add another valid interface or not
                            switch (this.prng.Next(addInterfaceWeight)) {

                                // add another interface to the list of valid interfaces
                                case 0:
                                case 1: {

                                        // check if there exists more interfaces that could be added to the list of valid interfaces
                                        if (basePossibleNode.givenClass.Interfaces.Count() == validPaths[validPathIdx].pathElements[idx].validInterfaces.Count()) {
                                            addAnotherInterface = false;
                                            break;
                                        }

                                        // add a random interface that is implemented by the base possible node
                                        int nextInterfaceIdx = this.prng.Next(basePossibleNode.givenClass.Interfaces.Count());
                                        while (true) {
                                            ITypeReference tempInterface = basePossibleNode.givenClass.Interfaces.ElementAt(nextInterfaceIdx);

                                            // check if chosen interface is already added to the list of valid interfaces
                                            // => try next interface implemented by the base possible node
                                            if (validPaths[validPathIdx].pathElements[idx].validInterfaces.Contains(tempInterface)) {
                                                nextInterfaceIdx = (nextInterfaceIdx + 1) % basePossibleNode.givenClass.Interfaces.Count();
                                            }

                                            // => add interface to list of valid interfaces
                                            else {
                                                validPaths[validPathIdx].pathElements[idx].validInterfaces.Add(tempInterface);

                                                // add interface to mandatory list (if it is not already in it)
                                                if (!validPaths[0].pathElements[idx].mandatoryInterfaces.Contains(tempInterface)) {
                                                    validPaths[0].pathElements[idx].mandatoryInterfaces.Add(tempInterface);
                                                }
                                                break;
                                            }
                                        }

                                        break;
                                    }

                                //do not add any more interfaces
                                default: {
                                        addAnotherInterface = false;

                                        break;
                                    }
                            }

                            // make adding another invalid interface more unlikely
                            addInterfaceWeight++;

                        }


                        // add invalid interfaces to the path element
                        addAnotherInterface = true;
                        addInterfaceWeight = 2;
                        while (addAnotherInterface) {

                            // decide wether to add another valid interface or not
                            switch (this.prng.Next(addInterfaceWeight)) {

                                // add another interface to the list of invalid interfaces
                                case 0:
                                case 1: {

                                        // check if there exists anymore interfaces that could be added to the list of invalid interfaces
                                        if (allInterfacesList.Count() <= (validPaths[validPathIdx].pathElements[idx].invalidInterfaces.Count() + validPaths[validPathIdx].pathElements[idx].validInterfaces.Count())) {
                                            if (allInterfacesList.Count() == (validPaths[validPathIdx].pathElements[idx].invalidInterfaces.Count() + validPaths[validPathIdx].pathElements[idx].validInterfaces.Count())) {
                                                addAnotherInterface = false;
                                                break;
                                            }

                                            throw new ArgumentException("The sum of valid and invalid interfaces should never be greater than the count of all interfaces.");
                                        }

                                        // check if there exists anymore interfaces that could be added to the list of invalid interfaces
                                        if (allInterfacesList.Count() <= (validPaths[validPathIdx].pathElements[idx].invalidInterfaces.Count() + basePossibleNode.givenClass.Interfaces.Count())) {
                                            if (allInterfacesList.Count() == (validPaths[validPathIdx].pathElements[idx].invalidInterfaces.Count() + basePossibleNode.givenClass.Interfaces.Count())) {
                                                addAnotherInterface = false;
                                                break;
                                            }

                                            throw new ArgumentException("The sum of implemented and invalid interfaces should never be greater than the count of all interfaces.");
                                        }

                                        // add a random interface that is NOT implemented by the base possible node
                                        int nextInterfaceIdx = this.prng.Next(allInterfacesList.Count());
                                        while (true) {
                                            ITypeReference tempInterface = allInterfacesList.ElementAt(nextInterfaceIdx);

                                            // check if chosen interface is NOT added to the list of invalid interfaces
                                            // and NOT implemented by the base possible node
                                            // => if it is try next interface of all interfaces
                                            if (validPaths[validPathIdx].pathElements[idx].invalidInterfaces.Contains(tempInterface)
                                                || basePossibleNode.givenClass.Interfaces.Contains(tempInterface)) {

                                                nextInterfaceIdx = (nextInterfaceIdx + 1) % allInterfacesList.Count();
                                            }

                                            // => add interface to list of invalid interfaces
                                            else {
                                                
                                                validPaths[validPathIdx].pathElements[idx].invalidInterfaces.Add(tempInterface);

                                                // add interface to forbidden list (if it is not already in it)
                                                if (!validPaths[0].pathElements[idx].forbiddenInterfaces.Contains(tempInterface)) {
                                                    validPaths[0].pathElements[idx].forbiddenInterfaces.Add(tempInterface);
                                                }

                                                break;
                                            }
                                        }

                                        break;
                                    }

                                //do not add any more interfaces
                                default: {
                                        addAnotherInterface = false;

                                        break;
                                    }
                            }

                            // make adding another invalid interface more unlikely
                            addInterfaceWeight++;

                        }
                    }
                }


                // => not the first element of the valid paths
                else {

                    // for each valid path add a path element
                    for (int validPathIdx = 0; validPathIdx < graphValidPathCount; validPathIdx++) {

                        // if the valid path id is not the first
                        // => search through all already chosen valid paths if the current path is the same until now
                        // and get the idx of this valid path (the first occurring is all that is needed)
                        int samePathIdx = -1;
                        if (validPathIdx != 0) {
                            for(int tempPathIdx = 0; tempPathIdx < validPathIdx; tempPathIdx++) {
                                bool samePath = true;
                                for(int depth = 0; depth <= idx; depth++) {
                                    if(validPaths[tempPathIdx].pathIndices[depth] != validPaths[validPathIdx].pathIndices[depth]) {
                                        samePath = false;
                                        break;
                                    }
                                }
                                if(samePath) {
                                    samePathIdx = tempPathIdx;
                                    break;
                                }
                            }
                        }


                        // if there does not exist a path that is the same up to this point
                        // => chose a random base possible node
                        PossibleNode basePossibleNode;
                        if (samePathIdx == -1) {

                            // get random base class that is used to generate the valid path element
                            ITypeReference baseInterface = allInterfacesList.ElementAt(this.prng.Next(allInterfacesList.Count()));
                            validPaths[validPathIdx].pathElements[idx].validInterfaces.Add(baseInterface);
                            List<PossibleNode> baseList = (List<PossibleNode>)this.graphInterfaces[baseInterface];
                            basePossibleNode = baseList.ElementAt(this.prng.Next(baseList.Count()));

                            // add interface to mandatory list (if it is not already in it)
                            if (!validPaths[validPathIdx].pathElements[idx].mandatoryInterfaces.Contains(baseInterface)) {
                                validPaths[validPathIdx].pathElements[idx].mandatoryInterfaces.Add(baseInterface);
                            }
                        }

                        // if there already exists a path
                        // => use the same base possible node
                        else {

                            basePossibleNode = currentRoundBasePossibleNodes[samePathIdx];

                            ITypeReference baseInterface = basePossibleNode.givenClass.Interfaces.ElementAt(this.prng.Next(basePossibleNode.givenClass.Interfaces.Count()));
                            validPaths[validPathIdx].pathElements[idx].validInterfaces.Add(baseInterface);

                            // add interface to mandatory list (if it is not already in it)
                            if (!validPaths[samePathIdx].pathElements[idx].mandatoryInterfaces.Contains(baseInterface)) {
                                validPaths[samePathIdx].pathElements[idx].mandatoryInterfaces.Add(baseInterface);
                            }

                        }


                        // add valid interfaces to the path element
                        bool addAnotherInterface = true;
                        int addInterfaceWeight = 3;
                        while (addAnotherInterface) {

                            // decide wether to add another valid interface or not
                            switch (this.prng.Next(addInterfaceWeight)) {

                                // add another interface to the list of valid interfaces
                                case 0:
                                case 1: {

                                        // check if there exists more interfaces that could be added to the list of valid interfaces
                                        if (basePossibleNode.givenClass.Interfaces.Count() == validPaths[validPathIdx].pathElements[idx].validInterfaces.Count()) {
                                            addAnotherInterface = false;
                                            break;
                                        }

                                        // add a random interface that is implemented by the base possible node
                                        int nextInterfaceIdx = this.prng.Next(basePossibleNode.givenClass.Interfaces.Count());
                                        while (true) {
                                            ITypeReference tempInterface = basePossibleNode.givenClass.Interfaces.ElementAt(nextInterfaceIdx);

                                            // check if chosen interface is already added to the list of valid interfaces
                                            // => try next interface implemented by the base possible node
                                            if (validPaths[validPathIdx].pathElements[idx].validInterfaces.Contains(tempInterface)) {
                                                nextInterfaceIdx = (nextInterfaceIdx + 1) % basePossibleNode.givenClass.Interfaces.Count();
                                            }

                                            // => add interface to list of valid interfaces
                                            else {
                                                validPaths[validPathIdx].pathElements[idx].validInterfaces.Add(tempInterface);

                                                // check if there exist a prior path that has an element at the same position
                                                // => if not, add interface to list of mandatory interfaces of this path
                                                if (samePathIdx == -1) {
                                                    if (!validPaths[validPathIdx].pathElements[idx].mandatoryInterfaces.Contains(tempInterface)) {
                                                        validPaths[validPathIdx].pathElements[idx].mandatoryInterfaces.Add(tempInterface);
                                                    }
                                                }

                                                // => if there is, add interface to list of mandatory interfaces of the prior path
                                                else {
                                                    if (!validPaths[samePathIdx].pathElements[idx].mandatoryInterfaces.Contains(tempInterface)) {
                                                        validPaths[samePathIdx].pathElements[idx].mandatoryInterfaces.Add(tempInterface);
                                                    }
                                                }

                                                break;
                                            }
                                        }

                                        break;
                                    }

                                //do not add any more interfaces
                                default: {
                                        addAnotherInterface = false;

                                        break;
                                    }
                            }

                            // make adding another invalid interface more unlikely
                            addInterfaceWeight++;

                        }


                        // add invalid interfaces to the path element
                        addAnotherInterface = true;
                        addInterfaceWeight = 2;
                        while (addAnotherInterface) {

                            // decide wether to add another valid interface or not
                            switch (this.prng.Next(addInterfaceWeight)) {

                                // add another interface to the list of invalid interfaces
                                case 0:
                                case 1: {

                                        // check if there exists anymore interfaces that could be added to the list of invalid interfaces
                                        if (allInterfacesList.Count() <= (validPaths[validPathIdx].pathElements[idx].invalidInterfaces.Count() + validPaths[validPathIdx].pathElements[idx].validInterfaces.Count())) {
                                            if (allInterfacesList.Count() == (validPaths[validPathIdx].pathElements[idx].invalidInterfaces.Count() + validPaths[validPathIdx].pathElements[idx].validInterfaces.Count())) {
                                                addAnotherInterface = false;
                                                break;
                                            }

                                            throw new ArgumentException("The sum of valid and invalid interfaces should never be greater than the count of all interfaces.");
                                        }

                                        // check if there exists anymore interfaces that could be added to the list of invalid interfaces 
                                        if (allInterfacesList.Count() <= (validPaths[validPathIdx].pathElements[idx].invalidInterfaces.Count() + basePossibleNode.givenClass.Interfaces.Count())) {
                                            if (allInterfacesList.Count() == (validPaths[validPathIdx].pathElements[idx].invalidInterfaces.Count() + basePossibleNode.givenClass.Interfaces.Count())) {
                                                addAnotherInterface = false;
                                                break;
                                            }

                                            throw new ArgumentException("The sum of implemented and invalid interfaces should never be greater than the count of all interfaces.");
                                        }


                                        // add a random interface that is NOT implemented by the base possible node
                                        int nextInterfaceIdx = this.prng.Next(allInterfacesList.Count());
                                        while (true) {
                                            ITypeReference tempInterface = allInterfacesList.ElementAt(nextInterfaceIdx);

                                            // check if chosen interface is NOT added to the list of invalid interfaces
                                            // and NOT implemented by the base possible node
                                            // => if it is try next interface of all interfaces
                                            if (validPaths[validPathIdx].pathElements[idx].invalidInterfaces.Contains(tempInterface)
                                                || basePossibleNode.givenClass.Interfaces.Contains(tempInterface)) {

                                                nextInterfaceIdx = (nextInterfaceIdx + 1) % allInterfacesList.Count();
                                            }

                                            // => add interface to list of invalid interfaces
                                            else {
                                                validPaths[validPathIdx].pathElements[idx].invalidInterfaces.Add(tempInterface);

                                                // check if there exist a prior path that has an element at the same position
                                                // => if not, add interface to list of forbidden interfaces of this path
                                                if (samePathIdx == -1) {
                                                    if (!validPaths[validPathIdx].pathElements[idx].forbiddenInterfaces.Contains(tempInterface)) {
                                                        validPaths[validPathIdx].pathElements[idx].forbiddenInterfaces.Add(tempInterface);
                                                    }
                                                }

                                                // => if there is, add interface to list of forbidden interfaces of the prior path
                                                else {
                                                    if (!validPaths[samePathIdx].pathElements[idx].forbiddenInterfaces.Contains(tempInterface)) {
                                                        validPaths[samePathIdx].pathElements[idx].forbiddenInterfaces.Add(tempInterface);
                                                    }
                                                }

                                                break;
                                            }
                                        }

                                        break;
                                    }

                                //do not add any more interfaces
                                default: {
                                        addAnotherInterface = false;

                                        break;
                                    }
                            }

                            // make adding another invalid interface more unlikely
                            addInterfaceWeight++;

                        }

                        // add current base possible node to the current round base possible nodes
                        currentRoundBasePossibleNodes[validPathIdx] = basePossibleNode;

                    }
                }
            }


            // set all mandatory and forbidden interfaces lists of all nodes that lie on the same path
            for (int idx = 0; idx < this.graphDepth; idx++) {

                for (int validPathIdx = 1; validPathIdx < graphValidPathCount; validPathIdx++) {

                    // search through all prior valid paths if the current path is the same until now
                    // and get the idx of this valid path (the first occurring is all that is needed)
                    int samePathIdx = -1;

                    for (int tempPathIdx = 0; tempPathIdx < validPathIdx; tempPathIdx++) {
                        bool samePath = true;
                        for (int depth = 0; depth <= idx; depth++) {
                            if (validPaths[tempPathIdx].pathIndices[depth] != validPaths[validPathIdx].pathIndices[depth]) {
                                samePath = false;
                                break;
                            }
                        }
                        if (samePath) {
                            samePathIdx = tempPathIdx;
                            break;
                        }
                    }

                    // if a path was found which is the same up to the current point
                    // => set the mandatory and forbidden list to the lists of the found path
                    if (samePathIdx != -1) {
                        validPaths[validPathIdx].pathElements[idx].mandatoryInterfaces = validPaths[samePathIdx].pathElements[idx].mandatoryInterfaces;
                        validPaths[validPathIdx].pathElements[idx].forbiddenInterfaces = validPaths[samePathIdx].pathElements[idx].forbiddenInterfaces;
                    }
                }
            }
            

            return validPaths;

        }

    }

}
