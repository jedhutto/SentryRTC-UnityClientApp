using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using Newtonsoft.Json;
using System.IO;

public class TableStorageRequestHandler : MonoBehaviour
{
    public enum Verb {GET, DELETE, POST, PUT};
    private string accountName;
    private string storageAccountKey;
    private string tableName;

    TableStorageRequestHandler()
    {
        GetAzureConfiguration();
    }

    void GetAzureConfiguration()
    {
        string path = "Assets/Resources/azure_configuration.userprefs";
        StreamReader reader = new StreamReader(path);
        accountName = reader.ReadLine();
        storageAccountKey = reader.ReadLine();
        tableName = reader.ReadLine();
        reader.Close();
    }

    public IEnumerable<(TableEntry, int)> SendRequest(Verb verb, TableEntry entry = null)
    {
        UnityWebRequest request;
        string uriParams = "";
        string body = "";
        if (entry != null)
        {
            uriParams = "(PartitionKey='" + entry.PartitionKey + "',RowKey='" + entry.RowKey + "')";
            if(verb != Verb.GET)
            {
                body = JsonConvert.SerializeObject(entry);
            }
        }
        var uri = new Uri("https://" + accountName + ".table.core.windows.net/" + tableName + uriParams);

        switch (verb)
        {
            case Verb.GET:
                request = UnityWebRequest.Get(uri);
                break;
            case Verb.DELETE:
                request = UnityWebRequest.Delete(uri);
                break;
            case Verb.POST:
                request = UnityWebRequest.PostWwwForm(uri, body);
                break;
            case Verb.PUT:
                request = UnityWebRequest.Put(uri, body);
                break;
            default:
                yield break;
        }

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Accept", "application/json;odata=nometadata");
        request.SetRequestHeader("x-ms-date", DateTime.UtcNow.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        request.SetRequestHeader("x-ms-version", "2020-04-08");
        request.SetRequestHeader("DataServiceVersion", "3.0;NetFx");
        request.SetRequestHeader("MaxDataServiceVersion", "3.0;NetFx");

        string stringToSign = request.GetRequestHeader("x-ms-date") + "\n";
        stringToSign += "/" + accountName + "/" + tableName + uriParams;
        System.Security.Cryptography.HMACSHA256 hasher = new System.Security.Cryptography.HMACSHA256(Convert.FromBase64String(storageAccountKey));
        string strAuthorization = "SharedKeyLite " + accountName + ":" + System.Convert.ToBase64String(hasher.ComputeHash(System.Text.Encoding.UTF8.GetBytes(stringToSign)));

        request.SetRequestHeader("Authorization", strAuthorization);
        request.SendWebRequest();

        while (!request.isDone)
        {

        }

        if (request.error != null && ((int)request.responseCode != 403 || (int)request.responseCode != 404))
        {
            Debug.Log(request.error);
        }
        else
        {
            //Debug.Log($"{verb} success");
        }
        TableEntry returnEntry = null;
        if(verb == Verb.GET)
        {
            returnEntry = JsonConvert.DeserializeObject<TableEntry>(request.downloadHandler.text);
        }

        int responseCode = (int)request.responseCode;
        request.Dispose();
        yield return (returnEntry, responseCode);
    }
}
