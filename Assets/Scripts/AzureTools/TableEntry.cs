using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Unity.VisualScripting;
using static UnityEngine.EventSystems.EventTrigger;

public class TableEntry
{
    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string description;
    [JsonProperty("candidate", NullValueHandling=NullValueHandling.Ignore)]
    public string candidate;
    [JsonProperty("status", NullValueHandling=NullValueHandling.Ignore)]
    public string status;
    [JsonProperty("RowKey")]
    public string RowKey;
    [JsonProperty("Timestamp", NullValueHandling=NullValueHandling.Ignore)]
    public string Timestamp; 
    [JsonProperty("PartitionKey")]
    public string PartitionKey;

    [OnSerializing]
    internal void OnSerializingMethod(StreamingContext context)
    { 
        description = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(description));
        candidate = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(candidate));
    }

    [OnDeserializing]
    internal void OnDeserializedMethod(StreamingContext context)
    {
        if(description != null)
            description = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(description));
        if (candidate != null)
            candidate = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(candidate));
    }

    public TableEntry(string name, string description = null, string candidate = null, string status = "standby")
    {
        this.description = description;
        this.status = status;
        this.candidate = candidate;
        PartitionKey = "unibottable";
        RowKey = name;
        Timestamp = DateTime.UtcNow.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }
}
