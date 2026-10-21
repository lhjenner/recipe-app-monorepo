using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class SupermarketChainConfiguration : IEntityTypeConfiguration<SupermarketChain>
{
    public void Configure(EntityTypeBuilder<SupermarketChain> chain)
    {
        chain.ToTable("SupermarketChains");
        chain.HasKey(entity => entity.Id);
        chain.Property(entity => entity.Code).IsRequired().HasMaxLength(100);
        chain.Property(entity => entity.Name).IsRequired().HasMaxLength(100);
        chain.HasIndex(entity => entity.Code).IsUnique();

        chain.HasData(
            new SupermarketChain
            {
                Id = Guid.Parse("360fab45-a7c0-4be5-88d8-bb5a272fc2d6"),
                Code = "woolworths",
                Name = "Woolworths"
            },
            new SupermarketChain
            {
                Id = Guid.Parse("19f8486e-7b2c-4e10-949f-ecf0d2039b36"),
                Code = "new-world",
                Name = "New World"
            },
            new SupermarketChain
            {
                Id = Guid.Parse("a2d39069-a4d6-4837-8b0b-7f0a858d552a"),
                Code = "pak-n-save",
                Name = "Pak'n Save"
            });
    }
}
