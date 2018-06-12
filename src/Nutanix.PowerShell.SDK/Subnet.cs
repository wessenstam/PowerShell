using System.Management.Automation;
using System;
using Newtonsoft.Json;

namespace Nutanix {

public class Subnet {
  public string Name { get; set; } = "";
  public string Id { get; set; } = "";

  // 'Uid' is VMware's equivalent field for Nutanix's Uuid.
  public string Uid;
  public string Uuid;
  public dynamic json { get; set; }

  // TODO Mtu, NumPorts, ExtensionData, NumPortsAvailable, Key, Nic, VMHostId,
  // VMHost, VMHostUid, Nic

  public Subnet(dynamic json) {
    // Special property 'json' stores the original json.
    this.json = json;
    this.json.Property("status").Remove();
    this.json.api_version = "3.1";

    Name = json.spec.name;
    Id = json.spec.resources.vlan_id;
    Uuid = json.metadata.uuid;
    Uid = Uuid;
  }
}

[CmdletAttribute(VerbsCommon.New, "VirtualSwitch")]
public class NewSubnetCmdlet : Cmdlet {
  [Parameter]
  public string Name { get; set; } = "";

  [Parameter]
  public string VlanId { get; set; } = "";

  [Parameter]
  public string Description { get; set; } = "";

  [Parameter]
  public Cluster Cluster { get; set; }

  protected override void ProcessRecord() {
    var url = "/subnets";
    var method = "POST";
    var str = @"{
      ""api_version"": ""3.0"",
      ""metadata"": {
        ""kind"": ""subnet"",
        ""name"": """ + Name + @"""
      },
      ""spec"": {
        ""description"": """ + Description + @""",
        ""name"": """ + Name + @""",
        ""resources"": {
          ""subnet_type"": ""VLAN"",
          ""vlan_id"": " + VlanId + @",
        }
      }
    }";
    dynamic json = JsonConvert.DeserializeObject(str);
    if (Cluster != null) {
      json.spec.cluster_reference = new Newtonsoft.Json.Linq.JObject();
      json.spec.cluster_reference.kind = "cluster";
      json.spec.cluster_reference.uuid = Cluster.Uuid;
      json.spec.cluster_reference.name = Cluster.Name;
    }

    WriteDebug(Util.RestCallTrace(url, method, json.ToString()));
    // TODO: should use Task.
    WriteObject(
      Task.FromUuidInJson(Util.RestCall(url, method, json.ToString())));
  }
}

[CmdletAttribute(VerbsCommon.Get, "VirtualSwitch")]
public class GetSubnetCmdlet : Cmdlet {
  // TODO: Name parameter to specify the names of subnets to retrieve.
  [Parameter]
  public string Uuid { get; set; } = "";

  [Parameter]
  public string Name { get; set; } = "";

  [Parameter]
  public int? Max { get; set; }

  protected override void ProcessRecord() {
    if (!String.IsNullOrEmpty(Uuid)) {
      WriteObject(GetSubnetByUuid(Uuid));
      return;
    }

    var subnets = GetAllSubnets(BuildRequestBody());
    CheckResult(subnets);
    WriteObject(subnets);
  }

  // Given the parameters, build request body for '/subnets/list'.
  public dynamic BuildRequestBody() {
    dynamic json = JsonConvert.DeserializeObject("{}");
    if (Max != null) {
      json.length = Max;
    }
    if (!String.IsNullOrEmpty(Name)) {
      json.filter = "name==" + Name;
    }
    return json;
  }

  public void CheckResult(Subnet[] subnets) {
    return; // TODO: consider whether throwing duplicate exception is good idea.
    if (!String.IsNullOrEmpty(Name) && subnets.Length > 1) {
      throw new Exception("Found duplicate subnets");
    }
  }

  public static Subnet GetSubnetByUuid(string uuid) {
    // TODO: validate using UUID regexes that 'uuid' is in correct format.
    var json = Util.RestCall("/subnets/" + uuid, "GET", "" /* requestBody */);
    return new Subnet(json);
  }

  public static Subnet[] GetSubnetsByName(string name) {
    return GetAllSubnets("{\"filter\": \"name==" + name + "\"}");
  }

  public static Subnet[] GetAllSubnets(dynamic jsonReqBody) {
    return GetAllSubnets(jsonReqBody.ToString());
  }

  public static Subnet[] GetAllSubnets(string reqBody) {
    return Util.FromJson<Subnet>(
      Util.RestCall("/subnets/list", "POST", reqBody),
      (Func<dynamic, Subnet>) (j => new Subnet(j)));
  }
}

[CmdletAttribute(VerbsCommon.Remove, "VirtualSwitch")]
public class DeleteSubnetCmdlet : Cmdlet {
  [Parameter]
  public string Uuid { get; set; } = "";

  // TODO: Confirm, WhatIf params.
  // https://www.vmware.com/support/developer/PowerCLI/PowerCLI41U1/html/Remove-VirtualSwitch.html

  protected override void ProcessRecord() {
    if (!String.IsNullOrEmpty(Uuid)) {
      // TODO: WriteObject Task
      DeleteSubnetByUuid(Uuid);
      return;
    }
  }

  public static void DeleteSubnetByUuid(string uuid) {
    // TODO: validate using UUID regexes that 'uuid' is in correct format.
    Util.RestCall("/subnets/" + uuid, "DELETE", "" /* requestBody */);
  }
}

[CmdletAttribute(VerbsCommon.Set, "VirtualSwitch")]
public class SetSubnetCmdlet : Cmdlet {
  [Parameter(Mandatory=true)]
  public Subnet Subnet { get; set; }

  [Parameter]
  public string Name { get; set; }

  [Parameter]
  public string VlanId { get; set; }

  protected override void ProcessRecord() {
    if (Name != null) {
      Subnet.json.spec.name = Name;
    }
    if (VlanId != null) {
      Subnet.json.spec.resources.vlan_id = VlanId;
    }
    Subnet.json.api_version = "3.1";
    Util.RestCall("/subnets/" + Subnet.Uuid, "PUT", Subnet.json.ToString());
  }

}

}
