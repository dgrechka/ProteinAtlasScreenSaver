module HpaScreenSaver.HpaClient

open System
open System.IO
open Angara.Data
open System.Net
open System.Xml.Schema
open FSharp.Data
open System.IO.Compression
open System.Net.NetworkInformation

let commonDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
let appDir = Path.Combine(commonDir, @"DgrechkaApps\HumanProteinAtlasScreenSaver");
let xmlCacheDir = Path.Combine(appDir, "xmlCache");
let imageCacheDir = Path.Combine(appDir, "imageCache");

let subcellularLocationsTableFile = Path.Combine(appDir,"subcellular_location.tsv")
let subcellularLocationsTableZippedFile = subcellularLocationsTableFile+".zip"

let subcellularLocationsURL = "https://www.proteinatlas.org/download/subcellular_location.tsv.zip"

if not (System.IO.Directory.Exists(appDir)) then
    System.IO.Directory.CreateDirectory(appDir) |> ignore

let xmlCacheSize = 128
let imageCacheSize = 1024

type Gene = {
    Gene: string
    ``Gene name``: string
    Reliability:string
    //Enhanced	Supported	Approved	Uncertain	Single-cell variation intensity	Single-cell variation spatial	Cell cycle dependency	GO id
}

type Hpa = XmlProvider<Schema = "proteinatlas.xsd">

type AsyncResult =
    |   Done
    |   Error of string

 type ImageInfo = {
    GeneName: string
    AntigenSeq: string
    Verification: string    
    CellLine:string
    ImageUrl: string
    LocalFileName: string
    IsCached: bool
    Technology: string
    Source: string    
    MainCellLocations: string[]
    AdditionalCellLocations: string[]
    OtherCellLocations: string[]
}
    

let downloadSubcellularLocationsAsync() =    
    async {
        try
            use wc = new WebClient()    
            do! Async.AwaitTask(wc.DownloadFileTaskAsync(subcellularLocationsURL,subcellularLocationsTableZippedFile))
            return Done
        with            
            ex ->
                if (File.Exists subcellularLocationsTableZippedFile) then
                    return Done
                else
                    return Error(sprintf "Error while downloading subcellular locations table: %s" (ex.ToString()))
        }
    
let unzipSubcellularLocationsAsync() =
    async {
        try
            let archive = ZipFile.Open(subcellularLocationsTableZippedFile, Compression.ZipArchiveMode.Read)
            let entry = Seq.head archive.Entries
            entry.ExtractToFile(subcellularLocationsTableFile,true)
            return Done
        with            
            ex ->
                if (File.Exists subcellularLocationsTableFile) then
                    return Done
                else
                    return Error(sprintf "Error while decompression subcellular locations table: %s" (ex.ToString()))
    }

let mutable geneTable: (Gene array) option = None
let mutable counter = 0

let initAsync() =
    async {
        let! downloadResult = downloadSubcellularLocationsAsync()
        match downloadResult with
        |   Error(er) -> return Error er
        |   Done ->
            let! dummy = unzipSubcellularLocationsAsync()
            let table:Table<Gene> = (Table.Load (subcellularLocationsTableFile,{ DelimitedFile.ReadSettings.Default with Delimiter=DelimitedFile.Delimiter.Tab })).ToRows<Gene>() |> Table.OfRows<Gene>
            geneTable <- Some(table.Rows |> Array.ofSeq)
            return Done
    }

let initTask() =
    Async.StartAsTask(initAsync())    

let getNewRandXmlPathAsync(genes:Gene array) =
    async {        
        let r = Random()

        if not (Directory.Exists xmlCacheDir) then
            Directory.CreateDirectory xmlCacheDir |> ignore

        let gene = genes.[r.Next(genes.Length)].Gene
    
        let filename = sprintf "%s.xml" gene
        let fullFilename = Path.Combine(xmlCacheDir, filename)        

        if File.Exists fullFilename then                
            return filename
        else            
            let link = sprintf "https://www.proteinatlas.org/%s" filename
            use wc = new WebClient()
            do! Async.AwaitTask(wc.DownloadFileTaskAsync(link,fullFilename))                    
            return fullFilename            
    }

let getCachedRandXmlPath() =
    let r = Random()
    let files = Directory.GetFiles(xmlCacheDir)
    files.[r.Next(files.Length)] 

let getImageInfos(xmlfileName:string) =
    let specificLocationChooser status (loc:Hpa.Location) =
        match loc.Status with
            |   None -> None
            |   Some(str) -> if str.ToLower() = status then Some(loc.Value) else None
        
    let otherLocationChooser toSkip (loc:Hpa.Location) =
        match loc.Status with
        |   None -> Some(loc.Value)
        |   Some(str) -> if (Seq.exists (fun p -> str.ToLower() = p) toSkip)  then None else Some(loc.Value)
        
    let extractFromEntry (entry:Hpa.Entry) =
        let geneName = entry.Name                
        let extractFromAntibody (ant:Hpa.Antibody) =
            let antigen =
                match ant.AntigenSequence with
                | Some(sequence) -> sequence.Value
                | None -> ""
            let extractFromCellExpression (exp:Hpa.CellExpression2) =
                let tech = exp.Technology
                let source = exp.Source
                let extractFromSubAssay (ass:Hpa.SubAssay) =
                    let verif =
                        match ass.Verification with
                        | None -> ""
                        | Some(verif) -> verif.Value
                    let extractFromData (data:Hpa.Data4) = 
                        let cellLine = data.CellLine                                
                        let mainLocations = data.Locations |> Array.choose (specificLocationChooser "main")
                        let additionalLocations = data.Locations |> Array.choose (specificLocationChooser "additional")
                        let otherLocations = data.Locations |> Array.choose (otherLocationChooser ["main";"additional"])
                        let urls =
                            match data.AssayImage with
                            | None -> []
                            | Some(image) ->
                                image.Images |> Seq.collect (fun x -> x.ImageUrls) |> Seq.toList
                        let generateEntry url : ImageInfo =
                            {
                                GeneName = geneName
                                MainCellLocations = mainLocations
                                AdditionalCellLocations = additionalLocations
                                OtherCellLocations = otherLocations
                                AntigenSeq = antigen
                                ImageUrl = url
                                IsCached = false
                                LocalFileName = Path.Combine(imageCacheDir,url.Remove(0,"http://v18.proteinatlas.org/images/".Length).Replace('/','-'))
                                CellLine = cellLine
                                Verification = verif
                                Technology = tech
                                Source = source
                            }
                        urls |> Seq.map generateEntry
                    ass.Datas |> Seq.collect extractFromData
                exp.SubAssays |> Seq.collect extractFromSubAssay
            ant.CellExpressions |> Seq.collect extractFromCellExpression
        entry.Antibodies |> Seq.collect extractFromAntibody
    
    let xml = Hpa.Load xmlfileName
    let entries = xml.Entries |> Seq.collect extractFromEntry
    entries

let getRandImageAsync() =
    async {        
        counter <- counter + 1
        try
            let genes =
                match geneTable with
                |   Some(genes) -> genes
                |   None -> failwith "genes table is not loaded"
            
            if(counter % 100 = 1) then
                // clearing XML cache
                let cachedFiles = Directory.GetFiles(xmlCacheDir);
                let filesCrTimes = cachedFiles |> Array.map (fun path -> path,(File.GetCreationTimeUtc path)) |> Array.sortByDescending snd |> Array.map fst
                let toDelete = 
                    if Array.length filesCrTimes > xmlCacheSize then
                        Array.skip xmlCacheSize filesCrTimes
                    else Array.empty
                Array.iter (fun file -> File.Delete file) toDelete
            
            if not (Directory.Exists imageCacheDir) then
                Directory.CreateDirectory imageCacheDir |> ignore
            
            if(counter % 100 = 5) then
                // clearing Image cache
                let cachedFiles = Directory.GetFiles(imageCacheDir);
                let filesCrTimes = cachedFiles |> Array.map (fun path -> path,(File.GetCreationTimeUtc path)) |> Array.sortByDescending snd |> Array.map fst
                let toDelete = 
                    if Array.length filesCrTimes > imageCacheSize then
                        Array.skip imageCacheSize filesCrTimes
                    else Array.empty
                Array.iter (fun file -> File.Delete file) toDelete

            let! xmlPath = getNewRandXmlPathAsync genes
            let images = getImageInfos xmlPath |> Array.ofSeq
            let r = Random()
            let image = 
                {
                    images.[r.Next(images.Length)] with
                        IsCached =false
                }
            

            use wc = new WebClient()
            do! Async.AwaitTask(wc.DownloadFileTaskAsync(image.ImageUrl,image.LocalFileName))
            return image
        with
            _ ->
                let xmlPath = getCachedRandXmlPath()
                let images = getImageInfos xmlPath |> Seq.filter (fun img -> File.Exists img.LocalFileName) |> Array.ofSeq
                let r = Random()
                let image = 
                    {
                        images.[r.Next(images.Length)] with
                            IsCached = true
                    }
                return image
    }
    
let getRandImageTask() =
    getRandImageAsync() |> Async.StartAsTask