using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WarehouseAI
{
    public class WarehouseRepresentation
    {
        public ItemDatabase ItemDatabase;
        private List<Item> _addedItems;

        private List<Node> _nodes;
        public Node[] Nodes => _nodes.ToArray();
        private WeightCache _cache;

        /// <summary>
        /// Imports the warehouse from a specific file.
        /// </summary>
        /// <param name="path">The path to the file</param>
        public void ImportWarehouse(string path)
        {
            CultureInfo c = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            c.NumberFormat.CurrencyDecimalSeparator = ".";

            string[][] lines = File.ReadAllLines(path).Select(s => s.Split(',').Select(t => t.Trim()).ToArray()).ToArray();

            _nodes = new List<Node>();

            foreach (string[] line in lines)
            {
                try
                {
                    Node newNode;

                    switch (line[1])
                    {
                        case "Node":
                            newNode = new Node();
                            break;
                        case "Shelf":
                            newNode = new Shelf();
                            break;
                        default:
                            newNode = new Node();
                            break;
                    }
                    newNode.Id = int.Parse(line[0]);
                    string[] pos = line[2].Split(' ');
                    newNode.X = float.Parse(pos[0], NumberStyles.Any, c);
                    newNode.Y = float.Parse(pos[1], NumberStyles.Any, c);

                    _nodes.Add(newNode);
                }
                catch { }
            }
            foreach (string[] line in lines)
            {
                try
                {
                    int id = int.Parse(line[0]);
                    string[] neighbours = line[3].Split(' ');

                    List<Node> neighbourNodes = new List<Node>();
                    foreach (string neighbour in neighbours)
                    {
                        try
                        {
                            neighbourNodes.Add(_nodes.Find(n => n.Id == int.Parse(neighbour)));
                        }
                        catch { }
                    }

                    Node node = _nodes.Find(n => n.Id == id);
                    node.Edges = neighbourNodes.Select(n => new Edge<Node> { from = node, to = n, weight = node.EuclidDistance(n) })
                        .ToArray();
                }
                catch { }
            }
        }

        /// <summary>
        /// Initializes the variables in the warehouse representation. Run this before taking any actions on the warehouse.
        /// </summary>
        public void Inintialize()
        {
            _addedItems = new List<Item>();
            _cache = new WeightCache(ItemDatabase.Items.Power().ToArray());
        }

        /// <summary>
        /// Adds a new node to the warehouse graph.
        /// </summary>
        /// <param name="newNode"></param>
        /// <param name="neighbourIds"></param>
        public void AddNode(Node newNode, int[] neighbourIds)
        {
            if (_nodes == null)
            {
                _nodes = new List<Node>();
            }

            List<Node> neighbourNodes = new List<Node>();
            foreach (int id in neighbourIds)
            {
                neighbourNodes.Add(_nodes.Find(n => n.Id == id));
            }

            newNode.Edges = neighbourNodes.Select(n => new Edge<Node>() { from = newNode, to = n, weight = newNode.EuclidDistance(n) })
                .ToArray();
            foreach (Node node in neighbourNodes)
            {
                node.Edges = node.Edges.Append(new Edge<Node>() { from = node, to = newNode, weight = node.EuclidDistance(newNode) }).ToArray();
            }

            newNode.Id = _nodes.Max(n => n.Id) + 1;

            _nodes.Add(newNode);
        }

        /// <summary>
        /// Adds a book to the warehouse. Calculates the optimal position and adds the book to the position.
        /// </summary>
        /// <param name="item"></param>
        public void AddBook(Item item)
        {
            //The filterNetwork is used as a tempoary graph where evaluations of the state are run on.
            //Only shelves and the dropoff point is added to the network.
            //Each node is a new "FilteredShelfNetworkNode", with an all pairs connection to eachother.
            Network<FilteredShelfNetworkNode> filterNetwork = new Network<FilteredShelfNetworkNode>(
                _nodes.ToArray(), n => n is Shelf,
                s => new FilteredShelfNetworkNode((Shelf)s, item));
            //The currentNode is any node in the graph, where greedy descent evaluations are completed on.
            FilteredShelfNetworkNode currentNode = filterNetwork.Nodes[0];

            //Add the new item to the added items list, if it is not already there.
            if (!_addedItems.Contains(item))
            {
                _addedItems.Add(item);
            }
            //The itemSets are the sets of items that need to be evaluated on.
            Item[][] itemSets = _addedItems.Power().Where(i => i.Contains(item)).ToArray();
            //lowestEvaluation denotes the lowest local evaulation of the evaluation funtion.
            float lowestEvaluation = float.MaxValue;
            //Whenever "cont" is not set back to "true" after running the while loop, new lowest local evaluations are still being found.
            bool cont = true;

            while (cont)
            {
                cont = false;

                //neighbours are the neighbouring nodes of the currentnode, where only "FilteredShelfNodes" are included,
                //the capacity for new items on the shelf are more than 0, and only including the 5 first.
                //Since the neighbours are ordered with lowest weight first, the 5 closest are chosen.
                FilteredShelfNetworkNode[] neighbours = currentNode.Neighbours.Where(n => n is FilteredShelfNetworkNode)
                    .Cast<FilteredShelfNetworkNode>().Where(n => n.Capacity > 0).Take(5).ToArray();

                foreach (FilteredShelfNetworkNode neighbour in neighbours)
                {
                    //Add the item to the neighbour...
                    neighbour.AddFilteredItem = true;
                    //Evaluate the state...
                    float eval = EvaluationFunction(filterNetwork, itemSets);
                    //And remove the item from the neighbour.
                    neighbour.AddFilteredItem = false;

                    //If the new evaluation is lower than the global lowest, the new evaluation is more efficient.
                    if (eval < lowestEvaluation)
                    {
                        currentNode = neighbour;
                        lowestEvaluation = eval;
                        cont = true;
                    }
                }
            }
            //After the while loop, currentNode has been set to be the local minimum, and the book is added to this shelf.
            ((Shelf)currentNode.Parent).AddBook(item);

            //Marks the item in the cache, making sure the weight of the item gets updated in the next evaluation
            //of the weight of the item.
            _cache.MarkItem(item);
        }

        /// <summary>
        /// Evaluates the current state of the network, and returns the efficency.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="network"></param>
        /// <param name="itemSets">The sets to evaluate</param>
        /// <returns></returns>
        private float EvaluationFunction<T>(Network<T> network, Item[][] itemSets) where T : INetworkNode
        {
            //Order the sets of items in the list to optimize the cache implementation
            itemSets = itemSets.OrderByDescending(i => i.Length).ToArray();
            //Create a new cache used in the specific evaulationfunction
            WeightCache cache = new WeightCache(itemSets);

            float result = 0;
            //Find the sum of the evaluation of each item set in itemSets
            foreach (Item[] set in itemSets)
            {
                result += EvaluateSet(network, cache, set);
            }

            return result;
        }

        /// <summary>
        /// Evaluate the state of the network with one specific set of items.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="network"></param>
        /// <param name="cache"></param>
        /// <param name="itemSet"></param>
        /// <returns></returns>
        private float EvaluateSet<T>(Network<T> network, WeightCache cache, Item[] itemSet) where T : INetworkNode
        {
            //The importance of the items in relation to eachother.
            float importance = Algorithms.Importance(itemSet);

            //To optimize, dont calculate the weight if the importance is 0.
            if (importance <= 0)
            {
                return 0;
            }

            //Calculate the weight given a set of nodes (the network), a cache, and a set of items.
            return importance * Algorithms.Weight(network.AllNodes.Cast<Node>().ToArray(), cache, itemSet);
        }
    }
}