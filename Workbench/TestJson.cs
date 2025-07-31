using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench
{
    internal class TestJson
    {
        public static string json = """
                    {
              "CanvasGuid": "3d18d198-1ef7-4de5-870c-a072da47c182",
              "Guid": "abe4ae47-b0e0-4616-afe1-621ec7b15370",
              "IsPublic": false,
              "AssemblyName": null,
              "MethodName": null,
              "Label": null,
              "Type": "Script",
              "PreviousNodes": {
                "Upstream": [],
                "IsSucceed": [
                  "24c74a00-974e-48b1-b0dc-ae8eb1cde7d4"
                ],
                "IsFail": [],
                "IsError": []
              },
              "SuccessorNodes": {
                "Upstream": [],
                "IsSucceed": [],
                "IsFail": [],
                "IsError": []
              },
              "TrueNodes": null,
              "FalseNodes": null,
              "UpstreamNodes": null,
              "ErrorNodes": null,
              "ParameterData": [
                {
                  "State": false,
                  "SourceNodeGuid": "24c74a00-974e-48b1-b0dc-ae8eb1cde7d4",
                  "SourceType": "GetOtherNodeData",
                  "ArgName": "user",
                  "Value": ""
                }
              ],
              "ParentNodeGuid": null,
              "ChildNodeGuids": [],
              "Position": {
                "X": 509.6,
                "Y": 351.7
              },
              "IsInterrupt": false,
              "IsEnable": true,
              "IsProtectionParameter": false,
              "CustomData": {
                "Script": "if (user.Info.Age >= 35) {\r\n user.Info.Name = \"[失业]\" + user.Info.Name;\r\n return user.Info.Name+\"  \"+user.Info.Age+\"岁\";\r\n} else {\r\n user.Info.Name = \"[牛马]\" + user.Info.Name;\r\n return user.Info.Name+\"  \"+user.Info.Age+\"岁\";\r\n}"
              }
            }
            """;

    }
}
