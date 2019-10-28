DNSMX
=====
Library and tools dedicated for scanning domains for MX records

# Repository

The repository contains library dedicated for scanning domains for MX records and related util and tool (see below), tests and pregenerated example input domains. Small description of directories:

  * `dag` - Domains Array Generator _(F#)_
  * `dnsmx` - Core library _(C#)_
  * `dnsmxcli` - DNS MX Command Line Interface (example and practical usage of `dnsmx` library) _(C#)_
  * `dnsmxtests` - Tests for `dnsmx` library _(F#)_
  * `domains` - pregenerated JSON array of many domains for testing

**dnsmx** was created in **.NET Core 3.0** (**C# 8.0** and **F# 4.7**). If you have any problems with compilation you can use pregenerated standalone self-contained exe files located in Releases.

# DnsMxCLI (DNS MX Command Line Interface)

The main point of repository is small tool dedicated for scanning domains for their MX records. For test purposes can be used large test list of domains located in `domains` directory (can be regenerated with usage of dag tool - see below). Example usage:
```cmd
dnsmxcli.exe --input domains.json
```
more practical usage:

```cmd
dnsmxcli.exe --input domains.json --output result.json --quiet --humanreadable
```
    
> `TIP` : dnsmxcli can be terminated during processing by pressing `Q` key

All arguments available for `dnsmxcli` tool.

```
  --quiet             Do not print any info into std output

  --sort              Sort all MX records for each domain via MX preference info

  --dnsip             Custom DNS IP address

  --dnsport           Custom DNS port

  --input             Specify file which contains JSON string array with domains

  --output            Final file location with result data (in JSON format)

  --humanreadable     Can be combined with --output option, the final data will be stored in "human readable" form

  --help              Display this help screen.

  --version           Display version information.

  Domains (pos. 0)    Input domains to process. Cannot be combined with --input option (otherwise will be simple
                      ignored)

```

# Domains Array Generator (DAG)

The repository contains utils tool called `dag` (Domains Array Generator) which can be used to create test input file with many domains (about ~20000) for dnsmxcli tool. Example usage:

```cmd
dag.exe --output domains.json
```

All arguments available for `dag` tool

```
USAGE: dag [--help] [--url <string>] [--output <string>] [--quiet]

OPTIONS:

    --url <string>        specify url to test-lists repository (default value is
                          https://github.com/citizenlab/test-lists/archive/master.zip).
    --output <string>     save result to file
    --quiet               quiet mode
    --help                display this list of options.
```
# About

(C) Copyright 2019 Maciej Izak