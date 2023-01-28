namespace LinkedList
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Runtime.Serialization;


    public class ListNode
    {
        public ListNode Prev;
        public ListNode Next;
        public ListNode Rand;
        public string Data;
    }


    public class ListRand
    {
        public ListNode Head;
        public ListNode Tail;
        public int Count;

        public void Serialize(FileStream s)
        {
            ListSerializer.Serialize(s, this);
        }

        public void Deserialize(FileStream s)
        {
            ListDeserializer.Deserialize(s, this);
        }
    }


    public static class ListSerializer
    {
        /// <summary>
        /// Serialize linked list and write to file stream.
        /// </summary>
        /// <param name="fs">File stream for writing serialized list</param>
        /// <param name="list">Linked list to serialize</param>
        public static void Serialize(FileStream fs, ListRand list)
        {
            Dictionary<ListNode, int> nodeToIndex = GetNodeToIndex(list);
            string listJson = GetListJson(list, nodeToIndex);
            byte[] bytes = new UTF8Encoding(true).GetBytes(listJson);
            fs.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Write list with all nodes to string in json format.
        /// Stop writing json when next node is null or hit cycle.
        /// </summary>
        /// <param name="list">List to write to json</param>
        /// <param name="nodeToIndex">Dictionary with nodes paired with their indexes</param>
        /// <returns>List in json format</returns>
        private static string GetListJson(ListRand list, Dictionary<ListNode, int> nodeToIndex)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            HashSet<ListNode> visitedNodes = new HashSet<ListNode>();
            ListNode node = list.Head;
            while (node != null)
            {
                visitedNodes.Add(node);
                WriteNode(node, sb, nodeToIndex);
                node = node.Next;
                if (node == null || visitedNodes.Contains(node))
                {
                    break;
                }

                sb.Append(",");
            }

            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>
        /// Write node's field values to string builder.
        /// References to prev, next and rand nodes written as indexes of corresponding node.
        /// </summary>
        /// <param name="node">Node which field values are written to string builder</param>
        /// <param name="sb">String builder to write to</param>
        /// <param name="nodeToIndex">Dictionary with nodes paired with their indexes</param>
        private static void WriteNode(ListNode node, StringBuilder sb, Dictionary<ListNode, int> nodeToIndex)
        {
            sb.Append("{");
            sb.Append($"Data='{node.Data}',");
            sb.Append($"Prev={(node.Prev == null ? "null" : nodeToIndex[node.Prev].ToString())},");
            sb.Append($"Next={(node.Next == null ? "null" : nodeToIndex[node.Next].ToString())},");
            sb.Append($"Rand={(node.Rand == null ? "null" : nodeToIndex[node.Rand].ToString())}");
            sb.Append("}");
        }

        /// <summary>
        /// Create a dictionary where each node is paired with it's index, if it was a simple array.
        /// Stop creating dictionary when next node is null or hit cycle.
        /// </summary>
        /// <param name="list">Linked list</param>
        /// <returns>Dictionary with nodes paired with their indexes</returns>
        private static Dictionary<ListNode, int> GetNodeToIndex(ListRand list)
        {
            Dictionary<ListNode, int> nodeToIndex = new Dictionary<ListNode, int>();
            ListNode node = list.Head;
            int index = 0;
            while (node != null && !nodeToIndex.ContainsKey(node))
            {
                nodeToIndex.Add(node, index++);
                node = node.Next;
            }

            return nodeToIndex;
        }
    }


    public struct NodeInfo
    {
        public readonly ListNode Node;
        public readonly string Prev;
        public readonly string Next;
        public readonly string Rand;

        public NodeInfo(ListNode node, string data, string prev, string next, string rand)
        {
            Prev = prev;
            node.Data = data;
            Next = next;
            Rand = rand;
            Node = node;
        }
    }


    namespace LinkedList.Exceptions
    {
        public class EmptyStreamException : SerializationException
        {
            public EmptyStreamException() : base("Stream is empty")
            {
            }
        }


        public class UnconnectedNodeException : SerializationException
        {
            public UnconnectedNodeException() : base("Found unconnected node")
            {
            }
        }
        public class UnexpectedCharException : SerializationException
        {
            public UnexpectedCharException(char c) : base($"Unexpected char: \'{c}\'''")
            {
            }
        }
        public class UnexpectedEndException : SerializationException
        {
            public UnexpectedEndException() : base("Unexpected end of stream")
            {
            }
        }
    }

    /// <summary>
    /// This class handles linked list deserialization.
    /// </summary>
    public static class ListDeserializer
    {
        
        /// <summary>
        /// Deserialize linked list from file stream.
        /// </summary>
        /// <param name="fs">File stream to read from</param>
        /// <param name="list">List to deserialize to</param>
        /// <exception cref="UnexpectedEndException">Thrown when reach end of stream before finished deserializing</exception>
        /// <exception cref="UnexpectedCharException">Thrown when found unexpected char</exception>
        public static void Deserialize(FileStream fs, ListRand list)
        {
            CheckStreamBeginning(fs);

            List<NodeInfo> nodeInfos = new List<NodeInfo>();
            bool readNode = false;
            while (true)
            {
                int b = fs.ReadByte();

                if (b == '{' && !readNode)
                {
                    readNode = true;
                    nodeInfos.Add(DeserializeNode(fs));
                }
                else if (b == ',' && readNode)
                {
                    readNode = false;
                }
                else if (b == -1)
                {
                    throw new LinkedList.Exceptions.UnexpectedEndException();
                }
                else if (b == ']')
                {
                    break;
                }
                else
                {
                    throw new LinkedList.Exceptions.UnexpectedCharException((char)b);
                }
            }

            FillList(list, nodeInfos);
        }

        /// <summary>
        /// Check stream not empty and starts with open bracket.
        /// </summary>
        /// <param name="fs">File stream to check</param>
        /// <exception cref="EmptyStreamException">Thrown if stream is empty or not starts with open bracket</exception>
        private static void CheckStreamBeginning(FileStream fs)
        {
            int firstByte = fs.ReadByte();
            if (firstByte == -1 || firstByte != '[')
            {
                throw new LinkedList.Exceptions.EmptyStreamException();
            }
        }

        /// <summary>
        /// Restore linked list fields and node references.
        /// </summary>
        /// <param name="list">Linked list to restore</param>
        /// <param name="nodeInfos">List of NodeInfo with nodes and their field values</param>
        private static void FillList(ListRand list, List<NodeInfo> nodeInfos)
        {
            if (nodeInfos.Count <= 0) return;
            ConnectNodes(nodeInfos);
            list.Head = nodeInfos.First().Node;
            list.Tail = nodeInfos.Last().Node;
            list.Count = nodeInfos.Count;
        }

        /// <summary>
        /// Restore node references.
        /// </summary>
        /// <param name="nodeInfos">List of NodeInfo with nodes and their field values</param>
        /// <exception cref="UnconnectedNodeException">Thrown when two or more nodes and one of them is not connected to others</exception>
        private static void ConnectNodes(List<NodeInfo> nodeInfos)
        {
            foreach (NodeInfo nodeInfo in nodeInfos)
            {
                if (nodeInfo.Next != "null")
                    nodeInfo.Node.Next = nodeInfos[int.Parse(nodeInfo.Next)].Node;
                if (nodeInfo.Prev != "null")
                    nodeInfo.Node.Prev = nodeInfos[int.Parse(nodeInfo.Prev)].Node;
                if (nodeInfo.Rand != "null")
                    nodeInfo.Node.Rand = nodeInfos[int.Parse(nodeInfo.Rand)].Node;
                if (nodeInfos.Count > 1 && nodeInfo.Node.Next == null && nodeInfo.Node.Prev == null)
                {
                    throw new LinkedList.Exceptions.UnconnectedNodeException();
                }
            }
        }

        /// <summary>
        /// Create node and read 4 fields with values from file stream and warp it into NodeInfo.
        /// </summary>
        /// <param name="fs">File stream to read from</param>
        /// <returns>NodeInfo instance with created node and their field values</returns>
        private static NodeInfo DeserializeNode(FileStream fs)
        {
            ListNode node = new ListNode();
            Dictionary<string, string> fields = new Dictionary<string, string>();
            for (int i = 0; i < 3; ++i)
            {
                ReadField(',', fs, fields);
            }

            ReadField('}', fs, fields);

            return new NodeInfo(node, fields["Data"], fields["Prev"], fields["Next"], fields["Rand"]);
        }

        /// <summary>
        /// Read field name and value from file stream and add to dictionary.
        /// </summary>
        /// <param name="stopChar">Char that separates field-value groups</param>
        /// <param name="fs">File stream to read from</param>
        /// <param name="fields">Dict to write field name and value to</param>
        private static void ReadField(char stopChar, FileStream fs, Dictionary<string, string> fields)
        {
            string line = ReadUntil(fs, stopChar);
            string[] parts = line.Split(new[] { '=' }, 2);
            fields.Add(parts[0], parts[1]);
        }

        /// <summary>
        /// Read from file stream until found stop char.
        /// If found apostrophe, then start reading until next apostrophe to get string value.
        /// </summary>
        /// <param name="fs">File stream to read from</param>
        /// <param name="stopChar">Char that stops reading</param>
        /// <returns>String that was read from file stream without stop char</returns>
        /// <exception cref="UnexpectedEndException">Thrown if reach end of stream before stop char</exception>
        private static string ReadUntil(FileStream fs, char stopChar)
        {
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                int b = fs.ReadByte();
                if (b == -1)
                {
                    throw new LinkedList.Exceptions.UnexpectedEndException();
                }

                if ((char)b == stopChar)
                {
                    break;
                }

                if (b == '\'')
                {
                    sb.Append(ReadUntil(fs, '\''));
                }
                else
                {
                    sb.Append((char)b);
                }
            }

            return sb.ToString();
        }
    }


    namespace LinkedListSample
    {
        internal class Program
        {
            static Random rand = new Random();

            static ListNode addNode(ListNode prev)
            {
                ListNode result = new ListNode();
                result.Prev = prev;
                result.Data = rand.Next(5000, 5100).ToString();
                prev.Next = result;
                return result;
            }

            public static void Main(string[] args)
            {
                SerializeSample();
                DeserializeSample();
            }

            private static void DeserializeSample()
            {
                ListRand list = new ListRand();
                ListNode curr;
                using FileStream fs = new FileStream("test.json", FileMode.Open);
                list.Deserialize(fs);
                curr = list.Head;
                while (curr.Next != null)
                {
                    if (curr.Rand == null)
                    {
                        Console.WriteLine("The element = " + curr.Data + ": it's random pointer value = null");
                        curr = curr.Next;
                    }
                    else
                    {
                        Console.WriteLine("The element = " + curr.Data + ": it's random pointer value = " + curr.Rand.Data);
                        curr = curr.Next;
                    }
                }
                if (curr.Rand == null)
                {
                    Console.WriteLine("The element = " + curr.Data + ": it's random pointer value = null");
                }
                else
                {
                    Console.WriteLine("The element = " + curr.Data + ": it's random pointer value = " + curr.Rand.Data);
                }
            }

            private static void SerializeSample()
            {
                int length = 10;
                ListNode[] lists = new ListNode[length];
                ListNode head = new ListNode();
                head.Prev = null;
                head.Data = rand.Next(5000, 5100).ToString();
                lists[0] = head;
                for (int i = 1; i < length; i++)
                {
                    ListNode current = addNode(lists[i - 1]);
                    lists[i] = current;
                }
                foreach (ListNode n in lists)
                {
                    if (rand.Next(0, 10) > 5)
                    {
                        n.Rand = null;
                    }
                    else
                    {
                        n.Rand = lists[rand.Next(0, 9)];
                    }
                }
                ListRand list = new ListRand();
                list.Head = lists[0];
                list.Tail = lists[length - 1];
                using FileStream fs = new FileStream("test.json", FileMode.OpenOrCreate);
                list.Serialize(fs);
            }
        }
    }

}