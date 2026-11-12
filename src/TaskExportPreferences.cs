// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System.Collections.Generic;
using System.Runtime.Serialization;

namespace LaoQin.ProgressManager {
 [DataContract] public sealed partial class TaskExportPreferences {
  [DataMember] public List<string> Order;
  [DataMember] public List<string> Selected;
  /// <summary>所选时间线类型；旧版没有该字段时默认全部选中。</summary>
  [DataMember(EmitDefaultValue=false)] public List<string> TimelineTypes;
  internal TaskExportPreferences Copy()=>new TaskExportPreferences{
   Order=Order==null?null:new List<string>(Order),Selected=Selected==null?null:new List<string>(Selected),
   TimelineTypes=TimelineTypes==null?null:new List<string>(TimelineTypes)
  };
 }
}
