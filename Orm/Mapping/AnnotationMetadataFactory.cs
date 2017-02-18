﻿using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lazaro.Orm.Attributes;

namespace Lazaro.Orm.Mapping
{
        public class AnnotationMetadataFactory : MetadataFactoryBase, IMetadataFactory
        {
                private static readonly ILog Log = LogManager.GetLogger(typeof(AnnotationMetadataFactory));

                public List<Assembly> Assemblies { get; set; } = new List<Assembly>();

                public AnnotationMetadataFactory()
                {
                }


                public void Init()
                {
                        /* foreach(var Asm in this.Assemblies) {
                                var ModelAttr = Asm.GetCustomAttributes(typeof(Attributes.Model), false);
                                if(ModelAttr.Length > 0) {
                                        Log.Info("Model found on " + Asm.Location);
                                        this.AddAssembly(Asm);
                                }
                        } */
                }


                public void ScanFolder(string folderName, bool recursive)
                {
                        var Files = System.IO.Directory.EnumerateFiles(folderName, "*.dll", recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly);
                        foreach(var File in Files) {
                                var Assembly = System.Reflection.Assembly.LoadFrom(File);
                                var ModelAttr = Assembly.GetCustomAttributes(typeof(Attributes.Model), false);
                                if (ModelAttr.Length > 0) {
                                        Log.Info("Model found on " + Assembly.Location);
                                        this.ScanAssembly(Assembly);
                                }
                        }
                }


                private void ScanAssembly(Assembly assembly)
                {
                        this.Assemblies.Add(assembly);

                        foreach (var Cls in assembly.GetExportedTypes()) {
                                var Attr = Cls.GetCustomAttributes(typeof(Attributes.Entity), false);
                                if (Attr.Length > 0) {
                                        var EntityAttr = Attr[0] as Attributes.Entity;
                                        Log.Info("Entity found on " + Cls.FullName);

                                        var ClsMeta = new Mapping.ClassMetadata()
                                        {
                                                Name = Cls.Name,
                                                TableName = EntityAttr.TableName,
                                                Columns = this.ScanClass(Cls)
                                        };
                                        ClsMeta.ObjectInfo = new Mapping.ObjectInfo(Cls, ClsMeta);

                                        if (string.IsNullOrWhiteSpace(EntityAttr.IdFieldName) == false) {
                                                // Overide id column name at table level, for compatibility with some ORMs
                                                ClsMeta.Columns.GetIdColumns()[0].Name = EntityAttr.IdFieldName;
                                        }

                                        this.ClassMetadata.Add(Cls.FullName, ClsMeta);
                                }
                        }


                        this.FillAssociationInfo();
                }
                

                private ColumnMetadataCollection ScanClass(Type clsType)
                {
                        var Res = new ColumnMetadataCollection();

                        var Members = clsType.GetMembers(BindingFlags.Instance | BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Public);
                        foreach(MemberInfo Mbr in Members) {
                                var ColAttrs = Mbr.GetCustomAttributes(typeof(Attributes.Column), true);
                                if(ColAttrs.Length == 1) {
                                        var NewCol = ColumnMetadataFromColumnAttribute(Mbr, ColAttrs[0] as Attributes.Column);

                                        var IdAttrs = Mbr.GetCustomAttributes(typeof(Attributes.Id), true);
                                        if (IdAttrs.Length == 1) {
                                                NewCol.Id = true;
                                        }

                                        var AssocAttrs = Mbr.GetCustomAttributes(typeof(Attributes.Association), true);
                                        if (AssocAttrs.Length == 1) {
                                                NewCol.AssociationMetada = AssociationMetadataFromAttribute(AssocAttrs[0] as Attributes.Association);
                                        }

                                        Res.Add(NewCol);
                                }

                        }

                        return Res;
                }

                private AssociationMetadata AssociationMetadataFromAttribute(Association assocAttr)
                {
                        var Res = new Mapping.AssociationMetadata();

                        if (assocAttr is Attributes.ManyToMany) {
                                Res.AssociationType = AssociationTypes.ManyToMany;
                        } else if (assocAttr is Attributes.ManyToOne) {
                                Res.AssociationType = AssociationTypes.ManyToOne;
                        } else if (assocAttr is Attributes.OneToMany) {
                                Res.AssociationType = AssociationTypes.OneToMany;
                        } else if (assocAttr is Attributes.OneToOne) {
                                Res.AssociationType = AssociationTypes.OneToOne;
                        }

                        return Res;
                }

                protected ColumnMetadata ColumnMetadataFromColumnAttribute(MemberInfo mbrInfo, Attributes.Column colAttr)
                {
                        var Res = new ColumnMetadata()
                        {
                                MemberName = mbrInfo.Name,
                                Id = colAttr.Id,
                                GeneratedValueStategy = colAttr.GeneratedValueStategy,
                                Name = colAttr.Name,
                                Type = colAttr.Type,
                                Length = colAttr.Length,
                                Precision = colAttr.Precision,
                                Nullable = colAttr.Nullable,
                                Unique = colAttr.Unique,
                        };

                        if (mbrInfo is PropertyInfo) {
                                Res.PropertyInfo = mbrInfo as PropertyInfo;
                                Res.MemberType = Res.PropertyInfo.PropertyType;
                        } else if (mbrInfo is FieldInfo) {
                                Res.FieldInfo = mbrInfo as FieldInfo;
                                Res.MemberType = Res.FieldInfo.FieldType;
                        }

                        return Res;
                }
        }
}
