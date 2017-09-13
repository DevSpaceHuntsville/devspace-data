#if !COMPILED
// Needed to resolve things automatically included by Azure Functions
#I "C:/Program Files (x86)/Azure/Functions/"
#r "Microsoft.Azure.WebJobs.Host"
#r "Microsoft.Azure.WebJobs.Extensions"
#endif

#r "Microsoft.WindowsAzure.Storage"

open System
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Host
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob

let Run (first0240:TimerInfo, log:TraceWriter) =
  log.Info( sprintf "MonthlyBackup function executed at: %s" (DateTime.Now.ToString()) )

  let storageConnString = System.Environment.GetEnvironmentVariable("devspacedatastorage")
  let latestBackupContainer = CloudStorageAccount.Parse(storageConnString).CreateCloudBlobClient().GetContainerReference("latest-backup")
  let monthlyBackupContainer = CloudStorageAccount.Parse(storageConnString).CreateCloudBlobClient().GetContainerReference("monthly-backup")

  let latestBackup =
    latestBackupContainer.ListBlobs()
    |> Seq.toList
    |> List.map (fun (b:IListBlobItem) -> b :?> CloudBlockBlob )
    |> List.sortByDescending (fun b -> b.Properties.LastModified.GetValueOrDefault( DateTimeOffset.MinValue ) )
    |> List.head

  monthlyBackupContainer.GetBlockBlobReference( latestBackup.Name ).StartCopy( latestBackup.Uri ) |> ignore

  // Delete old backups
  let backups =
    monthlyBackupContainer.ListBlobs()
    |> Seq.toList
    |> List.map (fun (b:IListBlobItem) -> b :?> CloudBlockBlob)
    |> List.sortBy (fun b -> b.Properties.LastModified.GetValueOrDefault( DateTimeOffset.MinValue ))

  let rec f (l:CloudBlockBlob list) =
    match l.Length with
    | x when x > 12 ->
      l.Head.Delete()
      f l.Tail
    | _ -> ()

  f backups