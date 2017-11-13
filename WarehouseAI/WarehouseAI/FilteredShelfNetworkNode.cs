using System.Linq;

namespace WarehouseAI
{
    public class FilteredShelfNetworkNode : NetworkNode
    {
        public new Shelf Parent;

        public bool AddFilteredItem = false;
        public int Capacity = 5;

        private Item _filterItem;
        
        public Item[] Items {
            get {
                return (AddFilteredItem
                    ? Parent.Items.Where(i => i != _filterItem).Append(_filterItem)
                    : Parent.Items.Where(i => i != _filterItem)).ToArray();
            }
        }

        public FilteredShelfNetworkNode(Shelf parent, Item filterItem) : base(parent)
        {
            _filterItem = filterItem;
        }
    }
}