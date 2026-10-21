using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> store)
    {
        store.ToTable("Stores");
        store.HasKey(entity => entity.Id);
        store.Property(entity => entity.Code).IsRequired().HasMaxLength(100);
        store.Property(entity => entity.Name).IsRequired().HasMaxLength(100);
        store.HasIndex(entity => entity.Code).IsUnique();
        store.HasOne(entity => entity.Chain)
            .WithMany(entity => entity.Stores)
            .HasForeignKey(entity => entity.ChainId)
            .OnDelete(DeleteBehavior.Restrict);

        store.HasData(
            new Store
            {
                Id = Guid.Parse("67af7315-c4e5-4f9e-89b3-7c3a71ba11da"),
                ChainId = Guid.Parse("19f8486e-7b2c-4e10-949f-ecf0d2039b36"),
                Code = "new-world-greenmeadows",
                Name = "New World Greenmeadows",
                DisplayOrder = 1
            },
            new Store
            {
                Id = Guid.Parse("b4df52fd-667a-41d8-9a9b-9d6e3bc8c87c"),
                ChainId = Guid.Parse("a2d39069-a4d6-4837-8b0b-7f0a858d552a"),
                Code = "pak-n-save-tamatea",
                Name = "Pak'n Save Tamatea",
                DisplayOrder = 2
            },
            new Store
            {
                Id = Guid.Parse("3e50666e-4aac-458f-9ae8-2eacba31ec6b"),
                ChainId = Guid.Parse("360fab45-a7c0-4be5-88d8-bb5a272fc2d6"),
                Code = "woolworths-carlyle",
                Name = "Woolworths Carlyle",
                DisplayOrder = 3
            });
    }
}
