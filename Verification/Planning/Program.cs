using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Entities;
using MagazzinoLegname.Persistence.Repositories;
using MagazzinoLegname.Services;
using MagazzinoLegname.ViewModels;
using Microsoft.EntityFrameworkCore;

static void Check(bool pass, string label) { if (!pass) throw new Exception("FAIL " + label); Console.WriteLine("PASS " + label); }
static void Conflict(Action action, string label) { try { action(); } catch(DbUpdateConcurrencyException) { Console.WriteLine("PASS " + label); return; } throw new Exception("FAIL expected conflict " + label); }
var (settings, _) = DatabaseSettingsLoader.Load();
if(settings.Database != "MagazzinoLegname_Dev") throw new Exception("Tests require Dev.");
var id = Guid.Parse(args[1]); var code = "TP-" + id.ToString("N")[..12];
var factory = new MagazzinoDbContextFactory(); var repo = new SqlPlannedArrivalRepository(factory);
DateTime date = new(2032, 5, 3);
PlannedArrival Draft(int offset = 0) => new() { SupplierId = id, Date = date.AddDays(offset), ConventionalThickness = 34, Quality = "C", LoadQuantity = 1, Notes = "TEST PIANIFICAZIONE " + code };
PlannedArrival Current(int offset = 0) => repo.GetAll().Single(x => x.SupplierId == id && x.Date == date.AddDays(offset));
decimal Standard() { using var db = factory.CreateDbContext(); return db.ApplicationSettings.Single().StandardCubicMetersPerExpectedLoad34; }
if(args[0] == "create") {
 using(var db = factory.CreateDbContext()) {
  var pending=db.Database.GetPendingMigrations().ToArray();
  Check(pending.All(x=>(x.EndsWith("_AddPlannedArrivals") || x.EndsWith("_AddPlannedConsumptions") || x.EndsWith("_RestoreWeeklyPlannedConsumptions"))), "migrations only planning tables");
  db.Database.Migrate(); Check(!db.Database.HasPendingModelChanges(), "EF model matches migration");
  db.Suppliers.Add(new SupplierEntity { Id=id, Code=code, Name="TEST PIANIFICAZIONE", IsActive=true }); db.SaveChanges();
 }
 var service = new PlanningDataService(repo); service.Reload(); var before=repo.GetAll().Count;
 service.GetOrCreateArrival(id,date); Check(repo.GetAll().Count==before && service.Arrivals.All(x=>x.SupplierId!=id), "empty cells are drafts only; no RAM authority/SQL rows");
 service.Save(Draft()); Check(Current().LoadQuantity==1 && Current().ExpectedCubicMeters==Standard() && Current().RowVersion.Length==8,"A create supplier/family34/C/1 load and reload");
 service.Save(Draft(8)); service.Save(Draft(17)); Check(repo.GetAll().Count(x=>x.SupplierId==id)==3,"D dates across different periods persisted");
}
else if(args[0] == "update") {
 Check(Current().LoadQuantity==1 && Current().ExpectedCubicMeters==Standard(),"A separate process restart preserves planning");
 var service = new PlanningDataService(repo); service.Reload(); var edit=Current(); var created=edit.CreatedAtUtc; edit.LoadQuantity=2; service.Save(edit);
 Check(Current().LoadQuantity==2 && Current().ExpectedCubicMeters==2*Standard() && Current().CreatedAtUtc==created && Current().UpdatedAtUtc>=created,"B update 1 to 2, coherent persisted MC, timestamps");
 var stale=Current(); var fresh=Current(); fresh.LoadQuantity=3; repo.Save(fresh);
 stale.LoadQuantity=4; Conflict(()=>repo.Save(stale),"E stale update rejected"); Conflict(()=>repo.Delete(stale),"stale delete rejected");
 try { service.Save(stale); throw new Exception("No service conflict"); } catch(InvalidOperationException ex) { Check(ex.Message.Contains("Conflitto") && service.Arrivals.Single(x=>x.Id==stale.Id).LoadQuantity==3,"service handles conflict and reloads winner"); }
 Conflict(()=>repo.Save(Draft()),"duplicate supplier/date insert rejected");
 using(var db=factory.CreateDbContext()) { db.Suppliers.Single(x=>x.Id==id).IsActive=false; db.SaveChanges(); }
 Check(Current().SupplierNameSnapshot=="TEST PIANIFICAZIONE","inactive supplier remains readable");
 try { repo.Save(Draft(1)); throw new Exception("Inactive accepted"); } catch(InvalidOperationException) { Console.WriteLine("PASS inactive supplier new arrival rejected"); }
 service.Reload(); service.ConfirmArrival(Current(8).Id,"TEST"); Check(Current(8).Status==PlannedArrivalStatus.Confirmed,"existing arrival confirmation persisted even for inactive supplier");
 repo.Delete(Current()); Check(repo.GetAll().All(x=>x.SupplierId!=id||x.Date!=date),"C delete controlled");
 using(var db=factory.CreateDbContext()) { db.Suppliers.Single(x=>x.Id==id).IsActive=true; db.SaveChanges(); }
}
else if(args[0] == "verify") {
 Check(repo.GetAll().All(x=>x.SupplierId!=id||x.Date!=date),"C delete survives separate process restart");
 Check(Current(8).Status==PlannedArrivalStatus.Confirmed && Current(17).LoadQuantity==1,"D other periods and confirmation survive restart");
 var vm=new PlanningViewModel(); vm.Refresh(); vm.SelectedWeekA=date; vm.SelectedWeekB=date.AddDays(7);
 Check(vm.CalendarWeeks.Count==2 && vm.CalendarWeeks.Select(x=>x.Label).SequenceEqual(new[]{"SETTIMANA A","SETTIMANA B"}) && vm.CalendarWeeks.All(x=>x.Days.Count==5 && x.Days.Select(d=>d.Date.DayOfWeek).SequenceEqual(new[]{DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday})) && vm.ForecastRows.Count==6 && vm.ForecastRows.All(x=>x.Weeks.Count==2),"UI model exactly two Mon-Fri weeks and six material rows with two weekly consumption cells");
 var saved=repo.GetAll().Where(x=>x.SupplierId==id).Select(x=>$"{x.Id}|{x.LoadQuantity}|{x.ExpectedCubicMeters}|{Convert.ToHexString(x.RowVersion)}").ToArray();
 vm.SelectedWeekA=date.AddDays(14); vm.SelectedWeekB=date.AddDays(21);
 Check(vm.CalendarWeeks.Count==2 && vm.CalendarWeeks[0].SupplierRows.SelectMany(x=>x.Cells).Any(x=>x.Arrival.SupplierId==id&&x.Arrival.Date==date.AddDays(17)&&x.Arrival.LoadQuantity==1),"D forward period loads SQL arrival in week A");
 vm.SelectedWeekA=date; vm.SelectedWeekB=date.AddDays(7);
 Check(saved.SequenceEqual(repo.GetAll().Where(x=>x.SupplierId==id).Select(x=>$"{x.Id}|{x.LoadQuantity}|{x.ExpectedCubicMeters}|{Convert.ToHexString(x.RowVersion)}")),"D period navigation preserves SQL values and RowVersion");
 Check(repo.GetAll().Count(x=>x.SupplierId==id)==2,"D period navigation never deletes data");
 Check(vm.CalendarWeeks[1].SupplierRows.SelectMany(x=>x.Cells).Any(x=>x.Arrival.SupplierId==id&&x.Arrival.Date==date.AddDays(8)&&x.Arrival.Status==PlannedArrivalStatus.Confirmed),"D backward period restores confirmed SQL arrival in week B");
}
else if(args[0] == "wpf") {
 Exception? failure=null;
 var thread = new Thread(() => {
  try {
   var app = new MagazzinoLegname.App(); app.InitializeComponent();
   var view = new MagazzinoLegname.Views.PlanningView();
   var vm = (PlanningViewModel)view.DataContext; vm.Refresh(); vm.SelectedWeekA=date; vm.SelectedWeekB=date.AddDays(7);
   view.Measure(new System.Windows.Size(1400,700)); view.Arrange(new System.Windows.Rect(0,0,1400,700)); view.UpdateLayout();
   System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
   Check(vm.CalendarWeeks.Count==2 && vm.ForecastRows.All(x=>x.Weeks.Count==2),"WPF compiled view loads exactly two weeks without exception");
   var cell=vm.CalendarWeeks[0].SupplierRows.SelectMany(x=>x.Cells).Single(x=>x.Arrival.SupplierId==id&&x.Arrival.Date==date);
   cell.Selection="34 C";
   System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
   Check(Current().LoadQuantity==1,"WPF cell selection INSERT/reload");
   cell=vm.CalendarWeeks[0].SupplierRows.SelectMany(x=>x.Cells).Single(x=>x.Arrival.SupplierId==id&&x.Arrival.Date==date);
   cell.LoadQuantity=2;
   System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
   Check(Current().LoadQuantity==2&&Current().ExpectedCubicMeters==2*Standard(),"WPF cell quantity UPDATE/reload");
   cell=vm.CalendarWeeks[0].SupplierRows.SelectMany(x=>x.Cells).Single(x=>x.Arrival.SupplierId==id&&x.Arrival.Date==date);
   cell.Selection="Nessun arrivo";
   System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
   Check(repo.GetAll().All(x=>x.SupplierId!=id||x.Date!=date),"WPF empty selection DELETE/reload");
   view.UpdateLayout(); app.Shutdown();
  } catch(Exception ex) { failure=ex; }
 });
 thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if(failure is not null)throw failure;
}
else if(args[0] == "cleanup") {
 using var db=factory.CreateDbContext(); db.PlannedArrivals.Where(x=>x.SupplierId==id).ExecuteDelete(); db.Suppliers.Where(x=>x.Id==id&&x.Code==code).ExecuteDelete(); Console.WriteLine("PASS fixture cleanup");
}
else throw new Exception("Unknown mode");
