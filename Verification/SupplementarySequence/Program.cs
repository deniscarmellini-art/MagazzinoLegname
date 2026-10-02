using System.Diagnostics;
using System.IO;
using System.Text.Json;
using MagazzinoLegname.Persistence;
using MagazzinoLegname.Persistence.Entities;
using MagazzinoLegname.Persistence.Repositories;
using MagazzinoLegname.Services;
using MagazzinoLegname.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

internal static class Program
{
 [STAThread]
 private static int Main(string[] args)
 {
  if(args.Length>0 && args[0]=="--restart")
  {
   var repo=new SqlInboundLoadRepository(new MagazzinoDbContextFactory());var data=repo.GetAll().Single(x=>x.Load.LoadNumber=="5-26");
   var p=repo.AddSupplementary(data.Load,data.Load.Groups.First(),"Fixture",DateTime.Now);
   Check(p.SupplementarySequence==4,"D nuovo processo: S04");return 0;
  }
  var (settings,_)=DatabaseSettingsLoader.Load();
  var name="MagazzinoLegname_TestSupplementary_"+Guid.NewGuid().ToString("N");
  settings.Database=name;settings.AllowedDatabases=[name];settings.Environment="Shared";
  var config=Path.Combine(AppContext.BaseDirectory,"fixture.settings.json");
  File.WriteAllText(config,JsonSerializer.Serialize(new DatabaseSettingsDocument{Database=settings}));
  Environment.SetEnvironmentVariable(DatabaseSettingsLoader.SettingsPathEnvironmentVariable,config);
  var factory=new MagazzinoDbContextFactory();
  try
  {
   string before;
   using(var db=factory.CreateDbContext())
   {
    db.GetService<IMigrator>().Migrate("20260930080203_RestoreWeeklyPlannedConsumptions");
    var supplier=new SupplierEntity{Id=Guid.NewGuid(),Code="TEST",Name="Fixture",IsActive=true};db.Suppliers.Add(supplier);
    foreach(var n in new[]{4,5,6})
    {
     var load=new LoadEntity{Id=Guid.NewGuid(),SupplierId=supplier.Id,LoadNumber=$"{n}-26",LoadYear=2026,AnnualProgressive=n,ArrivalDate=new DateTime(2026,10,1),ExpectedPackages=2};
     foreach(var thickness in new[]{44m,34m})
     {
      var width=thickness==44?190:180;var volume=10m*thickness*width*4000/1000000000m;
      var group=new MaterialGroupEntity{Id=Guid.NewGuid(),LoadId=load.Id,IncomingThickness=thickness,ConventionalThickness=thickness,IncomingWidth=width,WidthAfterPlaning=width,IncomingLength=4000,Quality="C",PackageCount=1,InitialPieces=10,IncomingPhysicalCubicMeters=volume,AppliedPrice=300,HistoricalValue=volume*300};
      load.MaterialGroups.Add(group);
      load.Packages.Add(new PackageEntity{Id=Guid.NewGuid(),LoadId=load.Id,MaterialGroupId=group.Id,PackageCode=$"OFF-{n}-{thickness}",QrPayload=$"OFF-{n}-{thickness}",PackageType=PersistentPackageType.Official,SequenceNumber=thickness==44?1:2,PieceCount=10,TotalOfficialPackages=2,Status="Presente",IncomingPhysicalCubicMeters=volume,AppliedPrice=300,HistoricalPackageValue=volume*300,ArrivalDate=load.ArrivalDate});
      if(n==4 && thickness==44)foreach(var sequence in new[]{1,2})load.Packages.Add(Supplement(load.Id,group.Id,n,sequence));
     }
     db.Loads.Add(load);
    }
    db.SaveChanges();before=OfficialSnapshot(db);
    var preserved=SupplementSnapshot(db);
    db.Database.Migrate();
    Check(SupplementSnapshot(db)==preserved,"Migration preserva codici, ID, QR e progressivi esistenti");
    Check(!db.Database.HasPendingModelChanges(),"Snapshot EF coerente con nuovo indice");
   }
   var repo=new SqlInboundLoadRepository(factory);var loads=repo.GetAll();var old=loads.Single(x=>x.Load.LoadNumber=="4-26");
   var continued=repo.AddSupplementary(old.Load,old.Load.Groups.Single(x=>x.IncomingThickness==34),"Fixture",DateTime.Now);
   Check(continued.PackageCode=="TEST-4-26-S03","Dati esistenti S01/S02: altro gruppo continua S03");
   var data=loads.Single(x=>x.Load.LoadNumber=="5-26");var a=data.Load.Groups.Single(x=>x.IncomingThickness==44);var b=data.Load.Groups.Single(x=>x.IncomingThickness==34);
   var s1=repo.AddSupplementary(data.Load,a,"Fixture",DateTime.Now);var s2=repo.AddSupplementary(data.Load,b,"Fixture",DateTime.Now);
   Check(s1.SupplementarySequence==1 && s2.SupplementarySequence==2 && s1.MaterialGroupId==a.GroupId && s2.MaterialGroupId==b.GroupId,"A gruppi diversi S01/S02 e gruppo corretto");
   var s3=repo.AddSupplementary(data.Load,a,"Fixture",DateTime.Now);Check(s3.SupplementarySequence==3,"B ritorno gruppo A: S03");
   using(var db=factory.CreateDbContext())before += "|"+db.Packages.Count();
   ClassificationWorkflowService.Shared.ReloadInboundLoads();
   var vm=new ClassificationViewModel();var labels=vm.GetSupplementaryPackages(a);var reprint=labels.Single(x=>x.Id==s1.Id);
   Check(reprint.PackageCode==s1.PackageCode && reprint.QrPayload==s1.QrPayload && reprint.SupplementarySequence==1 && reprint.OriginGroupId==a.GroupId,"C selezione ristampa usa S01 e QR esistenti");
   using(var db=factory.CreateDbContext())Check(before==OfficialSnapshot(db)+"|"+db.Packages.Count(),"C ristampa non inserisce pacchi/S04; pezzi MC valori ufficiali invariati");
   var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("--restart");
   using(var child=Process.Start(start)!){var output=child.StandardOutput.ReadToEnd();var error=child.StandardError.ReadToEnd();child.WaitForExit();Console.Write(output);Check(child.ExitCode==0,"D riavvio processo: "+error);}
   using var ready=new CountdownEvent(2);using var go=new ManualResetEventSlim();
   var tasks=new[]{a,b}.Select(group=>Task.Run(()=>{ready.Signal();go.Wait();return new SqlInboundLoadRepository(new MagazzinoDbContextFactory()).AddSupplementary(data.Load,group,"Fixture",DateTime.Now);})).ToArray();
   ready.Wait();go.Set();Task.WaitAll(tasks);var sequences=tasks.Select(t=>t.Result.SupplementarySequence).Order().ToArray();
   Check(sequences.SequenceEqual(new[]{5,6}) && tasks.Select(t=>t.Result.QrPayload).Distinct().Count()==2,"E due client concorrenti: S05/S06 e QR distinti");
   var other=loads.Single(x=>x.Load.LoadNumber=="6-26");Check(repo.AddSupplementary(other.Load,other.Load.Groups.First(),"Fixture",DateTime.Now).SupplementarySequence==1,"F altro carico riparte S01");
   using(var db=factory.CreateDbContext())
   {
    Check(before.Split('|')[0]==OfficialSnapshot(db),"Pezzi MC valori e rowversion ufficiali invariati");
    Check(db.Packages.Where(x=>x.PackageType==PersistentPackageType.Supplementary).All(x=>x.PieceCount==null && x.IncomingPhysicalCubicMeters==0 && x.AppliedPrice==null && x.HistoricalPackageValue==null),"Supplementari senza pezzi MC o valore");
    var bad=Supplement(data.Load.Id,b.GroupId,5,1);bad.PackageCode="UNIQUE-CODE-REPEATED-SEQUENCE";db.Packages.Add(bad);
    try{db.SaveChanges();throw new Exception("Indice per carico non applicato");}
    catch(DbUpdateException ex)when(ex.InnerException is SqlException sql && sql.Number is 2601 or 2627){Check(sql.Message.Contains("IX_Packages_LoadId_PackageType_SupplementarySequence"),"Nuovo indice SQL impedisce duplicato anche con codice diverso");}
   }
   Console.WriteLine("ALL A-F PASS; stampa verificata fino alla selezione del pacco, nessun invio alla stampante.");return 0;
  }
  catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
  finally{using var db=factory.CreateDbContext();if(db.Database.GetDbConnection().Database!=name)throw new Exception("Cleanup guard");db.Database.EnsureDeleted();Console.WriteLine("Fixture database removed");}
 }
 private static PackageEntity Supplement(Guid load,Guid group,int n,int sequence)=>new(){Id=Guid.NewGuid(),LoadId=load,MaterialGroupId=group,PackageCode=$"TEST-{n}-26-S{sequence:00}",QrPayload=$"TEST-{n}-26-S{sequence:00}",PackageType=PersistentPackageType.Supplementary,SupplementarySequence=sequence,Status="Presente",ArrivalDate=new DateTime(2026,10,1)};
 private static string OfficialSnapshot(MagazzinoDbContext db)=>JsonSerializer.Serialize(db.Packages.AsNoTracking().Where(x=>x.PackageType==PersistentPackageType.Official).OrderBy(x=>x.PackageCode).Select(x=>new{x.Id,x.PackageCode,x.QrPayload,x.PieceCount,x.IncomingPhysicalCubicMeters,x.AppliedPrice,x.HistoricalPackageValue,x.RowVersion}).ToArray());
 private static string SupplementSnapshot(MagazzinoDbContext db)=>JsonSerializer.Serialize(db.Packages.AsNoTracking().Where(x=>x.PackageType==PersistentPackageType.Supplementary).OrderBy(x=>x.PackageCode).Select(x=>new{x.Id,x.PackageCode,x.QrPayload,x.SupplementarySequence,x.MaterialGroupId,x.RowVersion}).ToArray());
 private static void Check(bool condition,string message){if(!condition)throw new Exception("FAIL "+message);Console.WriteLine("PASS "+message);}
}
