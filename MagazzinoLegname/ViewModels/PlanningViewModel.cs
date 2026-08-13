using System.Collections.ObjectModel;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class PlanningViewModel
{
    public ObservableCollection<MaterialPlanningParameter> WeeklyValues { get; } =
        PlanningParametersService.Shared.WeeklyValues;
}
