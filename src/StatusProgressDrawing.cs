// SPDX-License-Identifier: Apache-2.0
// Copyright 2026 老秦进度管理软件贡献者

using System;
using System.Xml.Linq;

namespace LaoQin.ProgressManager {
 // 使用 Excel 原生矢量形状，缩放和打印时保持清晰，节点随所属任务单元格移动。
 internal sealed class StatusProgressDrawing {
  static readonly XNamespace X="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
  static readonly XNamespace A="http://schemas.openxmlformats.org/drawingml/2006/main";
  public readonly XElement Root=new XElement(X+"wsDr",new XAttribute(XNamespace.Xmlns+"xdr",X),new XAttribute(XNamespace.Xmlns+"a",A));
  int sequence;
  static long Emu(double pixels){return (long)Math.Round(pixels*9525);}
  public static double MinimumHeight(TaskItem task){return Array.IndexOf(Model.ProgressStatuses,task.Status)<0?56:42;}
  public void Add(TaskItem task,int column,int row,double columnWidth,double heightPoints){
   int current=Model.ProgressIndex(task);double width=columnWidth*TaskExcelExporter.ColumnUnitPixels-16,step=width/Model.ProgressStatuses.Length;bool special=Array.IndexOf(Model.ProgressStatuses,task.Status)<0;
   const double diameter=18;double top=Math.Max(special?26:8,(heightPoints*4/3-41+(special?18:0))/2);
   for(int i=0;i<Model.ProgressStatuses.Length-1;i++){
    double left=8+step*(i+.5)+diameter/2;Shape(column,row,left,top+diameter/2-1,step-diameter,2,"rect",i<current?"8CB7A6":"DCE3ED",null,null,"连接线",false);
   }
   for(int i=0;i<Model.ProgressStatuses.Length;i++){
    bool reached=i<current,active=i==current;string color=active?(special?"A8742B":"4D65E6"):reached?"8CB7A6":"CBD5E1";
    double left=8+step*(i+.5)-diameter/2;
    Shape(column,row,left,top,diameter,diameter,"ellipse",reached||active?color:null,color,null,"节点-"+Model.ProgressStatuses[i]+(active?"-当前":reached?"-已完成":"-未完成"),false);
    Shape(column,row,8+step*i,top+diameter+3,step,20,"rect",null,null,Model.ProgressStatuses[i],"步骤-"+Model.ProgressStatuses[i],active,active?color:reached?"527C6D":"7B8597");
   }
  }
  void Shape(int column,int row,double left,double top,double width,double height,string geometry,string fill,string stroke,string text,string name,bool bold,string textColor="243247"){
   int id=++sequence;var shape=new XElement(X+"sp",new XElement(X+"nvSpPr",new XElement(X+"cNvPr",new XAttribute("id",id),new XAttribute("name",name+"-"+id)),new XElement(X+"cNvSpPr")),
    new XElement(X+"spPr",new XElement(A+"xfrm",new XElement(A+"off",new XAttribute("x",0),new XAttribute("y",0)),new XElement(A+"ext",new XAttribute("cx",Emu(width)),new XAttribute("cy",Emu(height)))),
     new XElement(A+"prstGeom",new XAttribute("prst",geometry),new XElement(A+"avLst")),fill==null?new XElement(A+"noFill"):Solid(fill),
     new XElement(A+"ln",new XAttribute("w",14288),stroke==null?new XElement(A+"noFill"):Solid(stroke))));
   if(text!=null)shape.Add(new XElement(X+"txBody",new XElement(A+"bodyPr",new XAttribute("wrap","none"),new XAttribute("lIns",0),new XAttribute("rIns",0),new XAttribute("tIns",0),new XAttribute("bIns",0),new XAttribute("anchor","ctr")),new XElement(A+"lstStyle"),
    new XElement(A+"p",new XElement(A+"pPr",new XAttribute("algn","ctr")),new XElement(A+"r",new XElement(A+"rPr",new XAttribute("lang","zh-CN"),new XAttribute("sz",1000),new XAttribute("b",bold?1:0),Solid(textColor),new XElement(A+"latin",new XAttribute("typeface","Microsoft YaHei")),new XElement(A+"ea",new XAttribute("typeface","Microsoft YaHei"))),new XElement(A+"t",text)))));
   Root.Add(new XElement(X+"oneCellAnchor",new XElement(X+"from",new XElement(X+"col",column),new XElement(X+"colOff",Emu(left)),new XElement(X+"row",row-1),new XElement(X+"rowOff",Emu(top))),new XElement(X+"ext",new XAttribute("cx",Emu(width)),new XAttribute("cy",Emu(height))),shape,new XElement(X+"clientData")));
  }
  static XElement Solid(string color){return new XElement(A+"solidFill",new XElement(A+"srgbClr",new XAttribute("val",color)));}
 }
}
