namespace Basket.API.Entities
{
    public class ShoppingCart
    {
        public string UserName { get; set; }
        public List<ShoppingCartItem> Items { get; set; } = new List<ShoppingCartItem>();

        public ShoppingCart(string username)
        {
            UserName = username;
        }

        public decimal TotalPrice
        {
            get
            {
                decimal TotalPrice = 0;
                foreach (var item in Items)
                {
                    TotalPrice += item.Price * item.Quantity;
                }
                return TotalPrice;
            }
        }
        public decimal CoolTotalPrice =>
            Items.Aggregate(default(decimal), (acc, curr) => acc + curr.Price * curr.Quantity);
    }

}