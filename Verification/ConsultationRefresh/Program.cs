using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MagazzinoLegname.Models;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Entities;
using MagazzinoLegname.Persistence.Repositories;
using MagazzinoLegname.Services;
using MagazzinoLegname.ViewModels;
using MagazzinoLegname.Views;
using MagazzinoLegname.Navigation;
using Microsoft.EntityFrameworkCore;

var (settings,_) = DatabaseSettingsLoader.Load();
if(settings.Database!="MagazzinoLegname_Dev") throw new Exception("Dev only");
Exception? failure=null;
var thread=new Thread(()=> {try {Run();} catch(Exception e){failure=e;}});
thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
if(failure!=null) throw new Exception("Verification failed",failure);
static void Check(bool pass,string name) { if(!pass) throw new Exception("FAIL "+name); Console.WriteLine("PASS "+name); }
static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject {
 for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {var c=VisualTreeHelper.GetChild(root,i); if(c is T match) yield return match; foreach(var x in Descendants<T>(c)) yield return x;}
}
static void Run() {
 var factory=new MagazzinoDbContextFactory(); var id=Guid.NewGuid(); var name="TF1-"+id.ToString("N")[..10];
 var app=new MagazzinoLegname.App(); app.InitializeComponent();
 var inbound=new SqlInboundLoadRepository(factory); var classes=new SqlClassificationRepository(factory);
 var terminals=new SqlPackageTerminalRepository(factory);
 try {
  using(var db=factory.CreateDbContext()) {
   Check(!db.Database.GetPendingMigrations().Any(),"no migration applied by tests");
   db.Suppliers.Add(new SupplierEntity {Id=id,Name=name,Code=name,IsActive=true});
   db.Operators.Add(new OperatorEntity {Id=id,FirstName=name,LastName="Test",IsActive=true}); db.SaveChanges();
  }
  var supplier=new SqlSupplierRepository(factory).GetAll().Single(x=>x.Id==id);
  var oper=new SqlOperatorRepository(factory).GetAll().Single(x=>x.Id==id);
  var navigation=new NavigationService(); UserControl? page=null; navigation.PageChanged+=(_,p)=>page=p;
  void Open(PageKey key) {navigation.NavigateTo(key); Check(((ConsultationViewModel)page!.DataContext).IsDataAvailable,"navigation SQL available "+key);}
  // Client B opens every cached page before client A writes; no Planning page is constructed.
  Open(PageKey.Dashboard); var dashboardView=page!; var dashboard=(DashboardViewModel)page!.DataContext;
  var beforeCount=dashboard.PresentPackages; var beforeVolume=dashboard.InventoryCubicMeters;
  Open(PageKey.Inventory); var inventoryView=page!; var inventory=(InventoryViewModel)page!.DataContext;
  Open(PageKey.History); var historyView=page!; var history=(HistoryViewModel)page!.DataContext;
  Open(PageKey.Statistics); var statsView=page!; var stats=(StatisticsViewModel)page!.DataContext;
  var line=new GoodsReceiptLine {PackageCount=1,PiecesPerPackage=10,IncomingThickness=34,IncomingWidth=180,IncomingLength=4000,Quality="C"};
  new GoodsReceiptCalculationService().Recalculate(line,null,MaterialParametersService.Shared.Parameters,100);
  var draft=new GoodsReceiptLoadDraft {Id=id}; draft.CaptureCertification("PEFC");
  // Client A: independent repository contexts, no shared-service notifications in client B.
  var load=inbound.Register(draft,supplier,oper,DateTime.Today,1,[line]);
  Open(PageKey.Dashboard);
  Check(ReferenceEquals(page,dashboardView) && dashboard.PresentPackages==beforeCount+1 && dashboard.InventoryCubicMeters==beforeVolume+line.PhysicalIncomingCubicMeters,"A/E cached Dashboard reload sees separate-context arrival and KPI");
  Open(PageKey.Inventory); Check(inventory.VisiblePackages.Any(x=>x.LoadId==id),"B initial package visible");
  Open(PageKey.Statistics); stats.SelectedSupplier=name;
  Check(stats.CubicMetersEntered==line.PhysicalIncomingCubicMeters && stats.TimePoints.Sum(x=>x.IncomingCubicMeters)==line.PhysicalIncomingCubicMeters,"D independent Statistics SQL refresh and chart");
  var group=load.Load.Groups.Single();
  var marked=classes.MarkClassified(id,group.GroupId,group.RowVersion,oper,DateTime.Now);
  var undone=classes.UndoClassification(id,group.GroupId,marked.RowVersion);
  marked=classes.MarkClassified(id,group.GroupId,undone.RowVersion,oper,DateTime.Now);
  var volume=line.PhysicalIncomingCubicMeters;
  var adjustment=new WasteAdjustment {LoadId=id,MaterialGroupId=group.GroupId,AdjustmentDate=DateTime.Now,AdjustmentOperator=oper.DisplayName,InitialPieces=10,DiscardedWholeBoards=0,GoodPieces=10,AdjustmentBaseCubicMeters=volume,CubicMetersBeforeAdjustment=volume,CubicMetersAfterWholeBoardWaste=volume,PartialWastePercentage=0,PartialWasteCubicMeters=0,RealAvailableCubicMeters=volume,WholeBoardWastePercentage=0,TotalClassificationWastePercentage=0};
  new SqlWasteAdjustmentRepository(factory).Add(id,group.GroupId,marked.RowVersion,adjustment,oper);
  terminals.Discharge(load.Packages.Single().PackageCode,oper.DisplayName);
  Open(PageKey.Inventory);
  Check(ReferenceEquals(page,inventoryView) && inventory.VisiblePackages.All(x=>x.LoadId!=id) && inventory.InventoryCubicMeters==beforeVolume,"B/E remote discharge disappears on cached-page navigation");
  Open(PageKey.History);
  var movements=history.VisibleMovements.Where(x=>x.LoadId==id).ToArray();
  Check(ReferenceEquals(page,historyView) && movements.Count(x=>x.MovementType=="Classificazione")==2 && new[]{"Entrata","Rettifica scarti","Scarico"}.All(t=>movements.Any(x=>x.MovementType==t)),"C/E full SQL classification history, adjustment and discharge");
  ClassificationWorkflowService.Shared.ClassificationHistory.Clear();
  Open(PageKey.History); Check(history.VisibleMovements.Count(x=>x.LoadId==id&&x.MovementType=="Classificazione")==2,"C session history cleared: full SQL history restored");
  Open(PageKey.Statistics); stats.SelectedSupplier=name;
  Check(ReferenceEquals(page,statsView) && stats.CubicMetersDischarged==volume && stats.TimePoints.Sum(x=>x.DischargedCubicMeters)==volume,"D/E remote discharge updates statistics and charts");
  bool oldOverflow=false;try{_=DateTime.MaxValue.Date.AddDays(1);}catch(ArgumentOutOfRangeException){oldOverflow=true;}
  Check(oldOverflow,"original null DateTo expression reproduces overflow");
  stats.SelectedPeriod="Personalizzato";stats.DateFrom=DateTime.Today;stats.DateTo=null;
  Check(stats.CubicMetersEntered==volume && stats.TimePoints.Count>0 && stats.TimePoints.Count<10,"DateTo null: unbounded filter, finite chart, no overflow");
  stats.DateTo=DateTime.Today; Check(stats.CubicMetersDischarged==volume,"DateTo populated includes whole day");
  stats.DateFrom=null;stats.DateTo=null;Check(stats.CubicMetersEntered==volume,"both dates null preserve data filtering");
  // Same compiled refresh button, not a second refresh implementation.
  foreach(var host in new[]{dashboardView,inventoryView,historyView,statsView}) {
   host.Measure(new Size(1600,1000));host.Arrange(new Rect(0,0,1600,1000));host.UpdateLayout();
   var button=Descendants<Button>(host).Single(b=>b.Content?.ToString()=="Aggiorna da SQL");
   button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   Check(((ConsultationViewModel)host.DataContext).IsDataAvailable,"explicit refresh "+host.GetType().Name);
  }
  foreach(var kind in new[]{0,1,2,3}) {
   var offline=false; Action reload=()=>{ if(offline) throw new TimeoutException("Injected repository timeout"); ConsultationSqlRefresh.Reload(); };
   ConsultationViewModel vm=kind switch {0=>new DashboardViewModel(reload),1=>new InventoryViewModel(reload),2=>new HistoryViewModel(reload),_=>new StatisticsViewModel(reload)};
   void Refresh(){switch(vm){case DashboardViewModel x:x.Refresh();break;case InventoryViewModel x:x.Refresh();break;case HistoryViewModel x:x.Refresh();break;case StatisticsViewModel x:x.Refresh();break;}}
   Refresh();Check(vm.IsDataAvailable,"F precondition "+kind);offline=true;Refresh();
   Check(!vm.IsDataAvailable && vm.RefreshMessage.Contains("Impossibile") && vm.RefreshMessage.Contains("riprovare"),"F failure hides stale data and explains retry "+kind);
   var host=new[]{dashboardView,inventoryView,historyView,statsView}[kind];host.DataContext=vm;
   host.Measure(new Size(1600,1000));host.Arrange(new Rect(0,0,1600,1000));host.UpdateLayout();
   var root=(DockPanel)host.Content;Check(((Border)root.Children[1]).Visibility==Visibility.Collapsed,"F compiled stale-content gate "+kind);
   offline=false;Refresh();Check(vm.IsDataAvailable,"F retry recovers "+kind);
  }
 } finally {
  using var strategyContext=factory.CreateDbContext();
  strategyContext.Database.CreateExecutionStrategy().Execute(()=> {
  using var db=factory.CreateDbContext();using var tx=db.Database.BeginTransaction();
  var packages=db.Packages.Where(x=>x.LoadId==id).Select(x=>x.Id).ToArray();
  db.PackageTerminalEvents.Where(x=>packages.Contains(x.PackageId)).ExecuteDelete();
  db.ClassificationMovements.Where(x=>x.LoadId==id).ExecuteDelete();db.WasteAdjustments.Where(x=>x.LoadId==id).ExecuteDelete();
  db.Packages.Where(x=>x.LoadId==id).ExecuteDelete();db.MaterialGroups.Where(x=>x.LoadId==id).ExecuteDelete();db.Loads.Where(x=>x.Id==id).ExecuteDelete();
  db.LoadNumberSequences.Where(x=>x.SupplierId==id).ExecuteDelete();db.SupplierThicknessConfigurations.Where(x=>x.SupplierId==id).ExecuteDelete();
  db.Suppliers.Where(x=>x.Id==id && x.Code==name).ExecuteDelete();db.Operators.Where(x=>x.Id==id && x.FirstName==name).ExecuteDelete();tx.Commit();
  });
  Console.WriteLine("Fixture cleanup completed");app.Shutdown();
 }
}
