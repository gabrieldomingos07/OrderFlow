using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Products;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(product => product.Id);
        builder.Property(product => product.Id).ValueGeneratedNever();

        builder.Property(product => product.Sku).HasMaxLength(Product.SkuMaxLength);
        builder.HasIndex(product => product.Sku).IsUnique();

        builder.Property(product => product.Name).HasMaxLength(Product.NameMaxLength);
        builder.Property(product => product.Description).HasMaxLength(Product.DescriptionMaxLength);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_products_price_positive", "price > 0");
            table.HasCheckConstraint("ck_products_stock_non_negative", "stock_quantity >= 0");
        });

        builder.HasXminConcurrencyToken();
    }
}
