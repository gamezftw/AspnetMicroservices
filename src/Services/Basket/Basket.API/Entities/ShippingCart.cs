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

        public decimal TotalPrice =>
            Items.Aggregate(default(decimal), (acc, curr) => acc + curr.Price * curr.Quantity);
    }

}