#if !COMPILED
// Needed to resolve things automatically included by Azure Functions
#I "C:/Program Files (x86)/Azure/Functions/"
#r "Microsoft.Azure.WebJobs.Host"
#r "Microsoft.Azure.WebJobs.Extensions"

// Needed to resolve references to nuget packages
#I "C:/Users/cgard/.nuget/packages/microsoft.sqlserver.dac/1.0.3/lib"
#r "Microsoft.SqlServer.Dac"
#endif

#r "Microsoft.WindowsAzure.Storage"

open System
open System.IO
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Host
open Microsoft.SqlServer.Dac
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob

let Run( daily0200:TimerInfo, log:TraceWriter ) =
  // Write start to log
  log.Info( sprintf "DailyBackup function executed at: %s" (DateTime.Now.ToString()) )

  let f = sprintf "DevSpace.%04i%02i%02i.bacpac" (DateTime.UtcNow.Year) (DateTime.UtcNow.Month) (DateTime.UtcNow.Day)
  let storageConnString = System.Environment.GetEnvironmentVariable("devspacedatastorage")
  let databaseConnString = System.Environment.GetEnvironmentVariable("devspacedatabase")

  // Create backup
  let ms = new MemoryStream()

  let dac = new DacServices( databaseConnString )
  dac.ExportBacpac( ms, "DevSpace" )
  ms.Seek( 0L,SeekOrigin.Begin ) |> ignore

  // Save Backup
  let latestBackupContainer = CloudStorageAccount.Parse( storageConnString ).CreateCloudBlobClient().GetContainerReference( "latest-backup" )
  latestBackupContainer.GetBlockBlobReference( f ).UploadFromStreamAsync(ms).Wait()

  // Delete old backups
  let backups =
    latestBackupContainer.ListBlobs()
    |> Seq.toList
    |> List.map (fun (b:IListBlobItem) -> b :?> CloudBlockBlob)
    |> List.sortBy (fun b -> b.Properties.LastModified.GetValueOrDefault( DateTimeOffset.MinValue ))

  let rec f (l:CloudBlockBlob list) =
    match l.Length with
    | x when x > 7 ->
      l.Head.Delete()
      f l.Tail
    | _ -> ()

  f backups