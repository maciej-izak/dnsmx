(*
 * (C) Copyright 2019 Maciej Izak
 *)

// simple domains JSON array generator

open System
open System.IO
open System.IO.Compression
open System.Text.Json
open System.Text.RegularExpressions
open FSharp.Data
open Argu

[<Literal>]
let defaultUrl = "https://github.com/citizenlab/test-lists/archive/master.zip"

type CLIArguments =
    | Url of string
    | Output of string
    | Quiet
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Url _ -> sprintf "specify url to test-lists repository (default value is %s)." defaultUrl
            | Output _ -> "save result to file"
            | Quiet _ -> "quiet mode"

[<EntryPoint>]
let main argv =
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<CLIArguments>(programName="dag", errorHandler=errorHandler)
    let results = parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)
    let url = if results.Contains(Url) then results.GetResult(Url)
              else defaultUrl
    let outputFile = results.TryGetResult(Output)
    let domainsCount = ref 0
    // Download file and extract all valuable CSV files. Return final output as JSON string array
    match Http.Request(url).Body with
    | Binary bytes ->
        use s = new MemoryStream(bytes)
        use a = new ZipArchive(s)
        a.Entries
        |> Seq.filter (fun f -> Regex.Match(f.FullName, @"test-lists-master\/lists\/\w+\.csv$").Success)
        |> Seq.collect (fun f ->
            CsvFile.Load(f.Open(), hasHeaders = true).Rows
            |> Seq.choose (fun r ->
                let m = Regex.Match(r.["url"],@"((http|https):\/\/)?(www\.)?([\w\.\-]+)");
                if m.Success then Some (m.Groups.[4].Value.ToLower()) else None))
        |> List.ofSeq
        |> List.distinct
        |> fun l -> domainsCount := l.Length; l
        |> JsonSerializer.Serialize
        |> fun s -> match outputFile with
                    | Some file -> File.WriteAllText(file, s)
                    | None -> printfn "%s" s
    | _ -> printfn "ERROR: binary file expected"; exit 1
    if not(results.Contains(Quiet)) then
        printfn "// Total number of generated unique domains = %i" !domainsCount
    0
