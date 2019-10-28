(*
 * (C) Copyright 2019 Maciej Izak
 *)

module Tests

open System.Collections.Generic
open Xunit
open DnsMx

[<Fact>]
let ``Test DnsMX process`` () =
    let domains = ["gmail.com"; "yahoo.com"]
    use f = new DnsMxFactory(domains)
    let r = f.Result
    Assert.Null r.Error
    let domainsArray = Array.ofSeq r.Domains
    Assert.Equal(domains.Length, domainsArray.Length)
    let domainsMap = domainsArray // check all domains
                     |> Array.map (fun d ->
                         Assert.True(d.MxDestCount > 0);
                         Assert.True(d.MxDestCount = d.MxProcessed);
                         Assert.Null d.Error;                         
                         (d.Domain, d))
                     |> Map.ofArray
    domains |> List.iter (domainsMap.ContainsKey >> Assert.True)
    domainsArray // check each MX record
    |> Array.collect (fun d -> d.MxArray)
    |> Array.iter (fun mx ->
        Assert.NotNull mx.Owner
        Assert.Null mx.Error
        Assert.NotNull mx.ExchangeIp
        Assert.NotEmpty mx.Exchange)

[<Fact>]
let ``Test DnsMX notify`` () =
    let domains = ["gmail.com"; "yahoo.com"]
    let notifyDict = ref (Dictionary<_,_>())
    use f = new DnsMxFactory(domains, (fun dmx -> (!notifyDict).Add(dmx.Domain, dmx)))
    Assert.Equal(domains.Length, (!notifyDict).Count)
    domains |> List.iter ((!notifyDict).ContainsKey >> Assert.True)

[<Fact>]
let ``Test improper dns`` () =
    let domains = ["gmail.com"]
    use f = new DnsMxFactory(domains, settings=FactorySettings(DnsIp="8.8.8.4"))
    Assert.NotNull(f.Result.Error)
    
[<Fact>]
let ``Test invalid dns`` () =
    let domains = ["gmail.com"]
    use f = new DnsMxFactory(domains, settings=FactorySettings(DnsIp="foo"))
    Assert.NotNull(f.Result.Error)

[<Fact>]
let ``Test Async cancel`` () =
    use f = new DnsMxFactory(["gmail.com"], settings=FactorySettings(Async = true))
    f.Cancel()
    let r = f.Process()
    Assert.NotNull r.Error
    let domainsArray = Array.ofSeq r.Domains
    Assert.Equal(1, domainsArray.Length)
    let d = domainsArray.[0]
    Assert.Equal(0, d.MxProcessed)
    Assert.NotNull d.Error