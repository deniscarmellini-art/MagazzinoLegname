using System.Collections.ObjectModel;
using System.Windows.Input;
using MagazzinoLegname.Infrastructure;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;

namespace MagazzinoLegname.ViewModels;

public sealed class QrPackagePreviewViewModel : ObservableObject
{
    private int _currentIndex;
    private readonly RelayCommand _previousCommand;
    private readonly RelayCommand _nextCommand;

    public QrPackagePreviewViewModel(IEnumerable<PhysicalPackageDraft> packages, string supplierName,
        string loadNumber, string deliveryNoteNumber, string certification)
    {
        var qrCodeService = new QrCodeService();
        Labels = new(packages.Select(package => new PackageLabelViewModel(package, supplierName,
            loadNumber, deliveryNoteNumber, certification, qrCodeService)));
        _previousCommand = new RelayCommand(() => CurrentIndex--, () => CurrentIndex > 0);
        _nextCommand = new RelayCommand(() => CurrentIndex++, () => CurrentIndex < Labels.Count - 1);
        PreviousCommand = _previousCommand; NextCommand = _nextCommand;
    }

    public ObservableCollection<PackageLabelViewModel> Labels { get; }
    public int CurrentIndex
    {
        get => _currentIndex;
        set
        {
            var bounded = Labels.Count == 0 ? 0 : Math.Clamp(value, 0, Labels.Count - 1);
            if (!SetProperty(ref _currentIndex, bounded)) return;
            OnPropertyChanged(nameof(CurrentLabel)); OnPropertyChanged(nameof(PositionText));
            _previousCommand.RaiseCanExecuteChanged(); _nextCommand.RaiseCanExecuteChanged();
        }
    }
    public PackageLabelViewModel? CurrentLabel => Labels.Count == 0 ? null : Labels[CurrentIndex];
    public string PositionText => Labels.Count == 0 ? "Nessuna etichetta" : $"Etichetta {CurrentIndex + 1} di {Labels.Count}";
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }
}
