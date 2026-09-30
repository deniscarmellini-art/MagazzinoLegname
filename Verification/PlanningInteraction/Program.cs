using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using MagazzinoLegname.Models;
using MagazzinoLegname.Services;
using MagazzinoLegname.ViewModels;
using MagazzinoLegname.Persistence.Repositories;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
var (settings, _) = DatabaseSettingsLoader.Load();
if (settings.Database != "MagazzinoLegname_Dev") throw new Exception("Dev only");
var id = Guid.Parse(args[1]);
using var db = new MagazzinoDbContextFactory().CreateDbContext();
if (args[0] == "prepare") {
 if (db.Database.GetPendingMigrations().Any()) throw new Exception("No migration allowed in crash test");
 db.Suppliers.Add(new SupplierEntity { Id=id, Code="TC-"+id.ToString("N")[..10], Name="ZZ TEST CRASH PIANIFICAZIONE", IsActive=true }); db.SaveChanges();
 Console.WriteLine("Fixture supplier ready " + id);
} else if (args[0] == "read") {
 foreach(var row in db.PlannedArrivals.AsNoTracking().Where(x=>x.SupplierId==id).OrderBy(x=>x.Date))
  Console.WriteLine($"SQL {row.Id} {row.Date:yyyy-MM-dd} {row.ConventionalThickness} {row.Quality} carichi={row.LoadQuantity} MC={row.ExpectedCubicMeters} RowVersion={Convert.ToHexString(row.RowVersion)}");
} else if (args[0] == "cleanup") {
 db.PlannedArrivals.Where(x=>x.SupplierId==id).ExecuteDelete(); db.Suppliers.Where(x=>x.Id==id && x.Name=="ZZ TEST CRASH PIANIFICAZIONE").ExecuteDelete(); Console.WriteLine("Fixture cleanup OK");
} else if (args[0] == "wpf" || args[0] == "restart") {
 Exception? failure=null;
 var thread=new Thread(()=> { try { RunWpf(id,args[0]=="restart"); } catch(Exception e) { failure=e; } });
 thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
 if(failure!=null) throw new Exception("WPF regression failed",failure);
} else throw new Exception("Unknown mode");

static void Check(bool ok,string name) { if(!ok) throw new Exception("FAIL "+name); Console.WriteLine("PASS "+name); }
static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject {
 for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {
  var child=VisualTreeHelper.GetChild(root,i); if(child is T match) yield return match;
  foreach(var item in Descendants<T>(child)) yield return item;
 }
}
static void RunWpf(Guid id,bool restart) {
 var app=new MagazzinoLegname.App(); app.InitializeComponent();
 var view=new MagazzinoLegname.Views.PlanningView(); var vm=(PlanningViewModel)view.DataContext;
 DateTime monday=new(2042,6,2);
 var repository=new SqlPlannedArrivalRepository(new MagazzinoDbContextFactory());
 void Pump() { Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle); view.Measure(new Size(1900,1000)); view.Arrange(new Rect(0,0,1900,1000)); view.UpdateLayout(); Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle); }
 PlannedArrival[] Rows()=>repository.GetAll().Where(x=>x.SupplierId==id && x.Date>=monday && x.Date<monday.AddDays(12)).ToArray();
 ComboBox Combo(int day)=>Descendants<ComboBox>(view).Single(x=>x.DataContext is PlanningArrivalCellViewModel c && c.Arrival.SupplierId==id && c.Arrival.Date==monday.AddDays(day));
 void Navigate(string label) { view.OnNavigatedTo(); Pump(); Check(Combo(0).SelectedItem?.ToString()==(Rows().FirstOrDefault(x=>x.Date==monday) is {} a ? $"{a.ConventionalThickness:0} {a.Quality}" : "Nessun arrivo"),"F reload/navigation "+label); }
 vm.Refresh(); vm.SelectedWeekA=monday; vm.SelectedWeekB=monday.AddDays(7); Pump();
 if(restart) { Check(Rows().Length==1 && Rows()[0].Date==monday.AddDays(7) && Combo(7).SelectedItem?.ToString()=="44 VISTA","G separate-process restart and bound UI retain week B arrival"); app.Shutdown(); return; }
 foreach(var leftover in Rows()) repository.Delete(leftover); view.OnNavigatedTo(); Pump();
 Check(Rows().Length==0,"clean regression dates");
 var first=Combo(0); var old=(PlanningArrivalCellViewModel)first.DataContext;
 first.SetCurrentValue(Selector.SelectedItemProperty,"34 C");
 Check(Rows().Length==0,"SQL not entered inside Selector selection transaction");
 old.Selection="34 C"; // duplicate notification before dispatcher commit
 Pump();
 Check(Rows().Length==1 && Rows()[0].LoadQuantity==1 && Combo(0).SelectedItem?.ToString()=="34 C","A real ComboBox binding inserts one arrival and rebuilds UI");
 var token=Rows()[0].RowVersion.ToArray(); old.LoadQuantity=8; Pump();
 Check(Rows()[0].RowVersion.SequenceEqual(token),"retired cell cannot save stale RowVersion"); Navigate("A");
 var quantity=Descendants<TextBox>(view).Single(x=>x.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path=="LoadQuantity" && x.DataContext is PlanningArrivalCellViewModel c && c.Arrival.SupplierId==id && c.Arrival.Date==monday);
 quantity.SetCurrentValue(TextBox.TextProperty,"2"); quantity.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); Pump();
 Check(Rows()[0].LoadQuantity==2 && vm.ForecastRows.Single(x=>x.MaterialLabel=="34 C").Weeks[0].ExpectedArrivals>=Rows()[0].ExpectedCubicMeters,"B quantity binding updates SQL and forecast"); Navigate("B");
 Combo(0).SetCurrentValue(Selector.SelectedItemProperty,"23 VISTA"); Pump();
 Check(Rows()[0].ConventionalThickness==23 && Rows()[0].Quality=="VISTA" && Rows()[0].LoadQuantity==2,"C material change preserves load count"); Navigate("C");
 Combo(0).SetCurrentValue(Selector.SelectedItemProperty,"Nessun arrivo"); Pump();
 Check(Rows().Length==0 && Combo(0).SelectedItem?.ToString()=="Nessun arrivo","D delete refreshes empty cell"); Navigate("D");
 Combo(7).SetCurrentValue(Selector.SelectedItemProperty,"44 VISTA"); Pump();
 Check(Rows().Length==1 && Rows()[0].Date==monday.AddDays(7) && Combo(7).SelectedItem?.ToString()=="44 VISTA","E week B insert"); Navigate("E");
 app.Shutdown();
}

