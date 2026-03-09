namespace MacharPos.Web.DTO
{
    public class SaleSyncDto
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public decimal TotalAmount { get; set; }

        public List<SaleItemSyncDto> Items { get; set; } = new();
    }

    public class SaleItemSyncDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }

        public int Cylinders { get; set; }
        public decimal Kg { get; set; }
        public decimal Price { get; set; }
    }
}
