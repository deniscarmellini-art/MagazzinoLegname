using System.IO;
using System.Globalization;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Entities;
using MagazzinoLegname.Persistence.Repositories;
using MagazzinoLegname.Services;
using MagazzinoLegname.ViewModels;
using Microsoft.EntityFrameworkCore;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
static void Check(bool condition, string label) { if (!condition) throw new Exception("FAIL " + label); Console.WriteLine("PASS " + label); }
static void Conflict(Action action, string label) { try { action(); } catch (DbUpdateConcurrencyException) { Console.WriteLine("PASS " + label); return; } throw new Exception("FAIL missing conflict: " + label); }
var (settings, _) = DatabaseSettingsLoader.Load();
if (settings.Database != "MagazzinoLegname_Dev") throw new Exception("Dev only");
var mode = args[0]; var fixture = Guid.Parse(args[1]);
Guid Key(byte salt) { var bytes = fixture.ToByteArray(); bytes[0] ^= salt; return new Guid(bytes); }
var code = "TN-" + fixture.ToString("N")[..12];
var date = new DateTime(2040, 5, 7); // Monday: two visible weeks, no third week.
var factory = new MagazzinoDbContextFactory();
var needs = new SqlPlannedConsumptionRepository(factory);
var arrivals = new SqlPlannedArrivalRepository(factory);
PlannedConsumption Draft(int week = 0, decimal amount = 20, decimal thickness = 34, string quality = "C") => new() {
    Id = Key((byte)(10 + week)), WeekStart = date.AddDays(7 * week), ConventionalThickness = thickness, Quality = quality, ExpectedCubicMeters = amount };
PlannedConsumption Current(int week = 0) => needs.GetAll().Single(x => x.WeekStart == date.AddDays(7 * week) && x.ConventionalThickness == 34 && x.Quality == "C");
PlanningDataService Service() { var service = new PlanningDataService(arrivals, needs); service.Reload(); return service; }
IReadOnlyList<PlannedConsumption> TestNeeds() => needs.GetAll().Where(x => x.WeekStart >= date && x.WeekStart < date.AddDays(12)).ToArray();
IReadOnlyList<PlannedArrival> TestArrivals() => arrivals.GetAll().Where(x => x.SupplierId == fixture).ToArray();
decimal Stock()
{
    ClassificationWorkflowService.Shared.ReloadInboundLoads();
    InventoryProjectionService.Shared.ReloadSqlTerminalMovements();
    return InventoryProjectionService.Shared.BuildInventory().Where(x => x.LoadId == fixture && x.IsPresent && x.IsAccountedPackage).Sum(x => x.InventoryCubicMeters);
}
IReadOnlyList<PlanningWeeklyForecast> Forecast(decimal thickness = 34, string quality = "C") => PlanningForecastCalculator.Calculate(
    Stock(), thickness, quality, date, date.AddDays(7), TestArrivals(), TestNeeds());
void DeleteFixture()
{
    using var db = factory.CreateDbContext();
    var keys = Enumerable.Range(0, 30).Select(x => Key((byte)x)).ToArray();
    db.PlannedConsumptions.Where(x => keys.Contains(x.Id)).ExecuteDelete();
    db.PlannedArrivals.Where(x => x.SupplierId == fixture).ExecuteDelete();
    db.Packages.Where(x => x.LoadId == fixture).ExecuteDelete();
    db.MaterialGroups.Where(x => x.LoadId == fixture).ExecuteDelete();
    db.SupplierReturnOperations.Where(x => x.LoadId == fixture).ExecuteDelete();
    db.Loads.Where(x => x.Id == fixture).ExecuteDelete();
    db.Operators.Where(x => x.Id == fixture && x.FirstName == code).ExecuteDelete();
    db.Suppliers.Where(x => x.Id == fixture && x.Code == code).ExecuteDelete();
}
if (mode == "inspect")
{
    using var db = factory.CreateDbContext();
    Console.WriteLine("Pending: " + string.Join(", ", db.Database.GetPendingMigrations()));
    Console.WriteLine("PlannedConsumptions rows: " + db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM dbo.PlannedConsumptions").Single());
    Console.WriteLine("WeekStart column: " + db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.columns WHERE object_id=OBJECT_ID('dbo.PlannedConsumptions') AND name='WeekStart'").Single());
}
else if (mode == "migrate")
{
    using var db = factory.CreateDbContext();
    var pending = db.Database.GetPendingMigrations().ToArray();
    Check(pending.Length == 1 && pending[0].EndsWith("_RestoreWeeklyPlannedConsumptions"), "only incremental weekly migration pending");
    var before = db.Database.SqlQueryRaw<PreviousConsumption>("SELECT * FROM dbo.PlannedConsumptions").ToArray();
    Check(!before.Any(x => x.Date >= date && x.Date < date.AddDays(12)), "migration fixture dates unused");
    var expected = before.GroupBy(x => (Monday: x.Date.AddDays(-(((int)x.Date.DayOfWeek + 6) % 7)), x.ConventionalThickness, x.Quality))
        .ToDictionary(x => x.Key, x => x.Sum(v => v.ExpectedCubicMeters));
    // This is solely a compatibility test of existing daily rows, not an operational daily forecast.
    for (int i = 0; i < 3; i++)
    {
        var id = Key((byte)(10 + i)); var sourceDate = date.AddDays(new[] { 1, 3, 8 }[i]); var amount = new[] { 12m, 8m, 30m }[i];
        db.Database.ExecuteSqlInterpolated($"INSERT INTO dbo.PlannedConsumptions (Id, [Date], ConventionalThickness, Quality, ExpectedCubicMeters, CreatedAtUtc, UpdatedAtUtc) VALUES ({id}, {sourceDate}, 34, N'C', {amount}, SYSUTCDATETIME(), SYSUTCDATETIME())");
    }
    try
    {
        db.Database.Migrate();
        var after = needs.GetAll();
        Check(after.All(x => x.WeekStart.DayOfWeek == DayOfWeek.Monday), "migration normalizes all dates to Monday");
        Check(expected.All(x => after.Single(y => y.WeekStart == x.Key.Monday && y.ConventionalThickness == x.Key.ConventionalThickness && y.Quality == x.Key.Quality).ExpectedCubicMeters == x.Value), "existing weekly totals preserved");
        Check(Current().ExpectedCubicMeters == 20 && Current(1).ExpectedCubicMeters == 30, "migration aggregates 12+8 into A=20; preserves B=30");
        Check(after.Count == expected.Count + 2 && !db.Database.HasPendingModelChanges(), "no duplicate weekly rows; EF snapshot aligned");
    }
    finally
    {
        var ids = new[] { Key(10), Key(11), Key(12) };
        foreach (var id in ids) db.Database.ExecuteSqlInterpolated($"DELETE FROM dbo.PlannedConsumptions WHERE Id={id}");
    }
}
else if (mode == "create")
{
    using (var db = factory.CreateDbContext())
    {
        Check(!db.Database.GetPendingMigrations().Any() && !db.Database.HasPendingModelChanges(), "weekly schema applied and EF snapshot aligned");
        Check(!db.PlannedConsumptions.Any(x => x.WeekStart >= date && x.WeekStart < date.AddDays(12)), "test weeks unused; existing data preserved");
        var index = db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.PlannedConsumptions') AND is_unique=1 AND name='IX_PlannedConsumptions_WeekStart_ConventionalThickness_Quality'").Single();
        var checks = db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.PlannedConsumptions') AND is_disabled=0").Single();
        Check(index == 1 && checks == 3, "SQL unique weekly index and three enabled CHECKs");
        db.Suppliers.Add(new SupplierEntity { Id = fixture, Code = code, Name = code, IsActive = true });
        db.Operators.Add(new OperatorEntity { Id = fixture, FirstName = code, LastName = "TEST", IsActive = true });
        var load = new LoadEntity { Id = fixture, SupplierId = fixture, LoadNumber = "1-40", LoadYear = 2040,
            AnnualProgressive = 1, ArrivalDate = DateTime.Today, ExpectedPackages = 4, Certification = "PEFC" };
        // Initial physical 200 m³, corrected to 100; terminal groups contribute zero.
        var group = new MaterialGroupEntity { Id = fixture, LoadId = fixture, ConventionalThickness = 34,
            IncomingThickness = 40, IncomingWidth = 250, WidthAfterPlaning = 245, IncomingLength = 10000,
            InitialPieces = 2000, PackageCount = 1, IncomingPhysicalCubicMeters = 200, Quality = "C", IsClassified = true, WasteVerified = true };
        group.WasteAdjustments.Add(new WasteAdjustmentEntity { Id = fixture, LoadId = fixture, MaterialGroupId = fixture,
            OperatorId = fixture, OperatorSnapshot = code + " TEST", OccurredAtUtc = DateTime.UtcNow.AddDays(-1), InitialPieces = 2000,
            GoodPieces = 2000, AdjustmentBaseCubicMeters = 200, CubicMetersBeforeAdjustment = 200, CubicMetersAfterWholeBoardWaste = 200,
            PartialWastePercentage = 50, PartialWasteCubicMeters = 100, RealAvailableCubicMeters = 100, TotalClassificationWastePercentage = 50 });
        group.ClassificationMovements.Add(new ClassificationMovementEntity { Id = fixture, LoadId = fixture, MaterialGroupId = fixture,
            OperatorId = fixture, OperatorSnapshot = code + " TEST", OccurredAtUtc = DateTime.UtcNow.AddDays(-2) });
        load.MaterialGroups.Add(group);
        load.Packages.Add(new PackageEntity { Id = fixture, LoadId = fixture, MaterialGroupId = fixture, PackageCode = code + "-1-40-1",
            SequenceNumber = 1, TotalOfficialPackages = 4, PieceCount = 2000, Status = "Presente", IncomingPhysicalCubicMeters = 200, ArrivalDate = DateTime.Today });
        load.Packages.Add(new PackageEntity { Id = Key(1), LoadId = fixture, MaterialGroupId = fixture, PackageCode = code + "-1-40-S01",
            PackageType = PersistentPackageType.Supplementary, SupplementarySequence = 1, Status = "Presente", ArrivalDate = DateTime.Today });
        for (int i = 0; i < 3; i++)
        {
            var groupId = Key((byte)(2 + i)); var packageId = Key((byte)(5 + i));
            load.MaterialGroups.Add(new MaterialGroupEntity { Id = groupId, LoadId = fixture, ConventionalThickness = 34, IncomingThickness = 40,
                IncomingWidth = 250, WidthAfterPlaning = 245, IncomingLength = 10000, InitialPieces = 500, PackageCount = 1,
                IncomingPhysicalCubicMeters = 50, Quality = "C" });
            load.Packages.Add(new PackageEntity { Id = packageId, LoadId = fixture, MaterialGroupId = groupId, PackageCode = $"{code}-1-40-{i + 2}",
                SequenceNumber = i + 2, TotalOfficialPackages = 4, PieceCount = 500, Status = "Scaricato", IncomingPhysicalCubicMeters = 50,
                ArrivalDate = DateTime.Today, TerminalEvent = new PackageTerminalEventEntity { Id = packageId, PackageId = packageId,
                    EventType = (PackageTerminalEventType)i, OperatorId = fixture, OperatorSnapshot = code + " TEST", OccurredAtUtc = DateTime.UtcNow,
                    InventoryCubicMeters = 50, ReturnedPhysicalCubicMeters = i == 1 ? 50 : null, Reason = "TEST",
                    ReturnOperation = i == 1 ? new SupplierReturnOperationEntity { Id = fixture, LoadId = fixture, OperatorId = fixture, OperatorSnapshot = code + " TEST", OccurredAtUtc = DateTime.UtcNow, Reason = "TEST" } : null } });
        }
        db.Loads.Add(load); db.SaveChanges();
    }

    Check(Stock() == 100, "SQL projection: 100 after correction; terminal/supplementary packages excluded");
    var service = Service(); var count = needs.GetAll().Count;
    var empty = service.GetOrCreateConsumption(date.AddDays(3), 34, "C");
    Check(empty.WeekStart == date && needs.GetAll().Count == count && service.Consumptions.Count == count, "empty week normalizes Monday without persisting a placeholder");
    service.Save(Draft()); service.Save(Draft(1, 30));
    using (var db = factory.CreateDbContext())
    {
        var offsets = new[] { 1, 3, 8 }; var volumes = new[] { 50m, 40m, 50m };
        for (int i = 0; i < 3; i++) db.PlannedArrivals.Add(new PlannedArrivalEntity { Id = Key((byte)(15 + i)), SupplierId = fixture,
            SupplierNameSnapshot = code, Date = date.AddDays(offsets[i]), ConventionalThickness = 34, Quality = "C", LoadQuantity = 1,
            ExpectedCubicMeters = volumes[i], CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        db.SaveChanges();
    }
    Check(Current().RowVersion.Length == 8 && Current(1).RowVersion.Length == 8, "both weekly needs saved with RowVersion");
    var result = Forecast();
    Check(result[0].OpeningBalance == 100 && result[0].ExpectedArrivals == 90 && result[0].ExpectedConsumption == 20 && result[0].ClosingBalance == 170, "A 100 + (Tuesday 50 + Thursday 40) - 20 = 170");
    Check(result[1].OpeningBalance == 170 && result[1].ExpectedArrivals == 50 && result[1].ExpectedConsumption == 30 && result[1].ClosingBalance == 190, "B 170 + 50 - 30 = 190");
}
else if (mode == "verify")
{
    Check(Current().ExpectedCubicMeters == 20 && Current(1).ExpectedCubicMeters == 30, "D separate process restart preserves A=20 / B=30 by WeekStart");
    var first = Current(); first.ExpectedCubicMeters = 40; Service().Save(first);
    var result = Forecast();
    Check(result[0].ClosingBalance == 150 && result[1].OpeningBalance == 150 && result[1].ClosingBalance == 170, "C A 20->40: final A=150; initial B=150; final B=170");
    Check(Forecast(34, "VISTA").All(x => x.ClosingBalance == 100) && new[] { 23m, 44m }.All(t => Forecast(t).All(x => x.ClosingBalance == 100)), "families and qualities remain independent");
    var extra = new PlannedArrival { Date = date.AddDays(5), SupplierId = fixture, ConventionalThickness = 34, Quality = "C", ExpectedCubicMeters = 999, LoadQuantity = 1 };
    var confirmed = new PlannedArrival { Date = date.AddDays(2), SupplierId = fixture, ConventionalThickness = 34, Quality = "C", ExpectedCubicMeters = 999, LoadQuantity = 1 };
    confirmed.Confirm(DateTime.Now, "TEST");
    Check(PlanningForecastCalculator.Calculate(100, 34, "C", date, date.AddDays(7), TestArrivals().Concat(new[] { extra, confirmed }), TestNeeds())[1].ClosingBalance == 170, "weekends and confirmed arrivals excluded");
    var stale = Current(); var fresh = Current(); fresh.ExpectedCubicMeters = 42; needs.Save(fresh);
    Conflict(() => needs.Save(stale), "stale weekly UPDATE rejected"); Conflict(() => needs.Delete(stale), "stale weekly DELETE rejected");
    var service = Service();
    try { service.Save(stale); throw new Exception("Missing conflict"); }
    catch (InvalidOperationException error) { Check(error.InnerException is DbUpdateConcurrencyException && error.Message.Contains("Conflitto") && service.Consumptions.Single(x => x.Id == stale.Id).ExpectedCubicMeters == 42, "conflict message and winner SQL reload"); }
    var reset = Current(); reset.ExpectedCubicMeters = 40; needs.Save(reset);
    var barrier = new Barrier(2);
    Task<bool> Insert(byte salt, decimal quantity) => Task.Run(() => {
        var client = Service(); var draft = new PlannedConsumption { Id = Key(salt), WeekStart = date, ConventionalThickness = 23, Quality = "VISTA", ExpectedCubicMeters = quantity };
        barrier.SignalAndWait();
        try { client.Save(draft); return true; }
        catch (InvalidOperationException error) when (error.InnerException is DbUpdateConcurrencyException) { Check(client.Consumptions.Single(x => x.WeekStart == date && x.ConventionalThickness == 23 && x.Quality == "VISTA").ExpectedCubicMeters is 11 or 22, "losing INSERT reloads winning weekly cell"); return false; }
    });
    var racers = new[] { Insert(20, 11), Insert(21, 22) }; Task.WaitAll(racers);
    Check(racers.Count(x => x.Result) == 1, "weekly unique index allows exactly one simultaneous INSERT");
    needs.Delete(needs.GetAll().Single(x => x.WeekStart == date && x.ConventionalThickness == 23 && x.Quality == "VISTA"));
    using (var db = factory.CreateDbContext())
    {
        db.PlannedConsumptions.Add(new PlannedConsumptionEntity { Id = Key(29), WeekStart = date.AddDays(1), ConventionalThickness = 23, Quality = "C", ExpectedCubicMeters = 10 });
        try { db.SaveChanges(); throw new Exception("Non-Monday accepted"); } catch (DbUpdateException) { Console.WriteLine("PASS SQL CHECK rejects non-Monday WeekStart"); }
    }
    try { needs.Save(new PlannedConsumption { WeekStart = date.AddDays(1), ConventionalThickness = 34, Quality = "C", ExpectedCubicMeters = 10 }); throw new Exception("Non-Monday accepted"); }
    catch (InvalidOperationException) { Console.WriteLine("PASS repository rejects non-Monday WeekStart"); }
    var broken = new BrokenNeeds(); var failing = new PlanningDataService(arrivals, broken); failing.Reload(); broken.Fail = true;
    try { failing.Reload(); throw new Exception("Missing SQL failure"); } catch (IOException) { Check(!failing.IsAvailable && failing.Consumptions.Count == 0 && failing.Arrivals.Count == 0, "SQL failure clears cache without RAM fallback"); }
}
else if (mode == "wpf")
{
    Exception? failure = null;
    var thread = new Thread(() => {
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
            var app = new MagazzinoLegname.App(); app.InitializeComponent();
            var listener = new BindingErrors(); System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            var view = new MagazzinoLegname.Views.PlanningView(); var vm = (PlanningViewModel)view.DataContext;
            vm.Refresh(); vm.SelectedWeekA = date; vm.SelectedWeekB = date.AddDays(7);
            void Layout() { view.Measure(new System.Windows.Size(1400, 700)); view.Arrange(new System.Windows.Rect(0, 0, 1400, 700)); view.UpdateLayout(); System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => {}, System.Windows.Threading.DispatcherPriority.ApplicationIdle); }
            PlanningForecastRowViewModel Row() => vm.ForecastRows.Single(x => x.MaterialLabel == "34 C");
            void Structure() => Check(vm.CalendarWeeks.Select(x => x.Label).SequenceEqual(new[] { "SETTIMANA A", "SETTIMANA B" })
                && vm.CalendarWeeks.All(x => x.Days.Select(d => d.Date.DayOfWeek).SequenceEqual(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }))
                && vm.ForecastRows.Select(x => x.MaterialLabel).SequenceEqual(new[] { "23 C", "23 VISTA", "34 C", "34 VISTA", "44 C", "44 VISTA" })
                && vm.ForecastRows.All(x => x.Weeks.Count == 2), "UI six rows / two weekly consumption cells; upper arrivals still daily A+B Mon-Fri");
            Layout(); Structure();
            var texts = Descendants<System.Windows.Controls.TextBlock>(view).Select(x => x.Text).ToArray();
            Check(new[] { "INIZIALE", "ARRIVI", "CONSUMO", "FINALE" }.All(label => texts.Count(x => x == label) == 12), "WPF exactly four weekly column captions per week/material");
            var editors = Descendants<System.Windows.Controls.TextBox>(view).Where(x => x.DataContext is PlanningForecastWeekViewModel).ToArray();
            Check(editors.Length == 12 && editors.All(x => System.Windows.Data.BindingOperations.GetBinding(x, System.Windows.Controls.TextBox.TextProperty)?.Path.Path == "QuantityText"), "WPF exactly 12 editable consumption fields; no daily needs");
            Check(editors.All(box => {
                System.Windows.DependencyObject? parent = box;
                while (parent is not null && !(parent is System.Windows.Controls.Grid g && g.DataContext is PlanningForecastRowViewModel))
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                var rowGrid = (System.Windows.Controls.Grid)parent!;
                return box.TranslatePoint(new System.Windows.Point(0, 0), rowGrid).Y + box.ActualHeight <= rowGrid.ActualHeight;
            }), "WPF consumption inputs fit compact rows without clipping");
            Check(Row().Weeks[0].ExpectedArrivals == 90 && Row().Weeks[1].ExpectedArrivals == 50 && Row().Weeks[1].OpeningBalance == Row().Weeks[0].ClosingBalance, "WPF weekly arrivals totals and A-to-B carry");
            var before = Row().Weeks[1].ClosingBalance; Row().Weeks[0].QuantityText = "45,50";
            Check(Current().ExpectedCubicMeters == 45.5m && Row().Weeks[1].ClosingBalance == before - 5.5m, "WPF SQL consumption edit recalculates both weeks");
            var token = Current().RowVersion;
            vm.SelectedWeekA = date.AddDays(14); vm.SelectedWeekB = date.AddDays(21); Structure();
            Check(vm.ForecastRows.All(x => x.Weeks.All(w => w.Consumption.RowVersion.Length == 0)), "E different period loads its own empty weekly cells");
            vm.SelectedWeekA = date; vm.SelectedWeekB = date.AddDays(7); Structure();
            Check(Row().Weeks[0].Consumption.ExpectedCubicMeters == 45.5m && Row().Weeks[1].Consumption.ExpectedCubicMeters == 30 && Current().RowVersion.SequenceEqual(token), "E back navigation restores exact WeekStart quantities and token");
            using (var db = factory.CreateDbContext()) { db.WasteAdjustments.Single(x => x.Id == fixture).RealAvailableCubicMeters = 90; db.SaveChanges(); }
            before = Row().Weeks[0].OpeningBalance; vm.Refresh();
            Check(Row().Weeks[0].OpeningBalance == before - 10, "page refresh reads actual SQL inventory");
            using (var db = factory.CreateDbContext()) { db.WasteAdjustments.Single(x => x.Id == fixture).RealAvailableCubicMeters = 100; db.SaveChanges(); }
            vm.Refresh(); Row().Weeks[0].QuantityText = "40";
            // Isolated unused material tests INSERT, blank DELETE and zero DELETE through actual TextBox bindings.
            PlanningForecastWeekViewModel EmptyCell() => vm.ForecastRows.Single(x => x.MaterialLabel == "23 VISTA").Weeks[0];
            void Enter(string text) { Layout(); var box = Descendants<System.Windows.Controls.TextBox>(view).Single(x => ReferenceEquals(x.DataContext, EmptyCell())); box.Text = text; box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)!.UpdateSource(); }
            Enter("5,25"); Check(EmptyCell().Consumption.ExpectedCubicMeters == 5.25m, "WPF TextBox binding weekly INSERT/reload");
            Enter(""); Check(EmptyCell().Consumption.RowVersion.Length == 0, "WPF TextBox blank controlled DELETE");
            Enter("5"); Enter("0"); Check(EmptyCell().Consumption.RowVersion.Length == 0, "WPF TextBox zero controlled DELETE");
            Layout(); Check(listener.Errors.Count == 0, "WPF bindings no errors: " + string.Join(" / ", listener.Errors));
            if (args.Length > 2) { var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1400, 700, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(view);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); using var file = File.Create(args[2]); encoder.Save(file); }
            app.Shutdown();
        } catch (Exception error) { failure = error; }
    }); thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if (failure is not null) throw failure;
}
else if (mode == "verifyfinal")
{
    Check(Current().ExpectedCubicMeters == 40 && Current(1).ExpectedCubicMeters == 30, "D second restart preserves updated weekly consumptions");
    var service = Service(); service.Delete(Current()); service.Delete(Current(1));
    Check(!TestNeeds().Any(), "controlled weekly deletion reloads SQL");
}
else if (mode == "cleanup") { DeleteFixture(); Console.WriteLine("PASS fixture cleanup"); }
else throw new Exception("Unknown mode");

static IEnumerable<T> Descendants<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
{
    for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
    {
        var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
        if (child is T match) yield return match;
        foreach (var nested in Descendants<T>(child)) yield return nested;
    }
}
sealed class PreviousConsumption
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public decimal ConventionalThickness { get; set; }
    public string Quality { get; set; } = "";
    public decimal ExpectedCubicMeters { get; set; }
}
sealed class BrokenNeeds : IPlannedConsumptionRepository
{
    public bool Fail { get; set; }
    public IReadOnlyList<PlannedConsumption> GetAll() => Fail ? throw new IOException("Simulated SQL unavailable") : new[] { new PlannedConsumption { WeekStart = new DateTime(2040, 5, 7), ConventionalThickness = 34, Quality = "C", ExpectedCubicMeters = 10 } };
    public void Save(PlannedConsumption consumption) => throw new NotSupportedException();
    public void Delete(PlannedConsumption consumption) => throw new NotSupportedException();
}
sealed class BindingErrors : System.Diagnostics.TraceListener
{
    public List<string> Errors { get; } = [];
    public override void Write(string? message) { if (!string.IsNullOrWhiteSpace(message)) Errors.Add(message); }
    public override void WriteLine(string? message) => Write(message);
}
