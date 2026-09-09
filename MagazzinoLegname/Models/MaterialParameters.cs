using System.Collections.ObjectModel;
using MagazzinoLegname.Infrastructure;

namespace MagazzinoLegname.Models;

public sealed class MaterialParameters : ObservableObject
{
    public MaterialParameters()
    {
        ThicknessFamilies =
        [
            new(20m, 29m, 23m),
            new(30m, 39m, 34m),
            new(40m, 49m, 44m)
        ];
    }
    public ObservableCollection<ThicknessFamilyConfiguration> ThicknessFamilies { get; }
    public ThicknessFamilyConfiguration? FindFamily(decimal incomingThickness) =>
        ThicknessFamilies.FirstOrDefault(family => family.Includes(incomingThickness));
}
