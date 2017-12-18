using System.Collections.Generic;

namespace Town
{
    public class Link
    {
        private readonly float _price;
        public float Price => NeedsBridge ? _price * 1000 : _price;
        public bool NeedsBridge { get; set; }

        public Link(float price, bool needsBridge = false)
        {
            _price = price;
            NeedsBridge = needsBridge;
        }
    }
    public class Node
    {
        private static int _counter;
        public Dictionary<Node, Link> Links = new Dictionary<Node, Link>();
        public readonly int Id;

        public Node()
        {
            Id = _counter++;
        }

        public void Link(Node node, float price = 1f, bool symmetrical = true, bool needsBridge = false)
        {
            Links[node] = new Link(price, needsBridge);
            if (symmetrical)
            {
                node.Links[this] = new Link(price, needsBridge);
            }
        }

        public void Unlink(Node node, bool symmetrical = true)
        {
            Links.Remove(node);
            if (symmetrical)
            {
                node.Links.Remove(this);
            }
        }

        public void UnlinkAll()
        {
            Links.Clear();
        }

        public override string ToString()
        {
            return $"Node({Id})";
        }
    }
}