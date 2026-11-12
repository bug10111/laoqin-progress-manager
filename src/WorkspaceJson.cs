// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System.IO;
using System.Runtime.Serialization.Json;

namespace LaoQin.ProgressManager {
 internal static class WorkspaceJson {
  static DataContractJsonSerializer Serializer(){
   return new DataContractJsonSerializer(typeof(AppData),new DataContractJsonSerializerSettings{MaxItemsInObjectGraph=10000000});
  }
  internal static byte[] Encode(AppData data){
   using(var stream=new MemoryStream()){
    Serializer().WriteObject(stream,data);
    return stream.ToArray();
   }
  }
  internal static AppData Decode(byte[] bytes){
   using(var stream=new MemoryStream(bytes)){
    var data=(AppData)Serializer().ReadObject(stream);
    Model.Check(data);
    return data;
   }
  }
 }
}
