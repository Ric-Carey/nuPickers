﻿namespace nuPickers.Shared.DotNetDataSource
{
    using DataSource;
    using nuPickers.Shared.Editor;
    using Paging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Umbraco.Core.Logging;

    public class DotNetDataSource : IDataSource
    {
        private bool handledTypeahead = false;

        public string AssemblyName { get; set; }

        public string ClassName { get; set; }

        public IEnumerable<DotNetDataSourceProperty> Properties { get; set; }

        bool IDataSource.HandledTypeahead { get { return this.handledTypeahead; } }

        IEnumerable<EditorDataItem> IDataSource.GetEditorDataItems(int currentId, int parentId, string typeahead)
        {
            return this.GetEditorDataItems(currentId == 0 ? parentId : currentId, typeahead); // fix from PR #110
        }

        IEnumerable<EditorDataItem> IDataSource.GetEditorDataItems(int currentId, int parentId, string[] keys)
        {
            return this.GetEditorDataItems(currentId == 0 ? parentId : currentId).Where(x => keys.Contains(x.Key));
            //TODO: update public IDotNetDataSource so keys can be passed though (so it can do a more efficient query)
        }

        IEnumerable<EditorDataItem> IDataSource.GetEditorDataItems(int currentId, int parentId, PageMarker pageMarker, out int total)
        {
            //HACK: paging implemented here until IDotNetDataSourcePage has been implemented
            var editorDataItems = this.GetEditorDataItems(currentId == 0 ? parentId : currentId);

            total = editorDataItems.Count();

            return editorDataItems.Skip(pageMarker.Skip).Take(pageMarker.Take);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contextId">'contextId' implies that it could be the current node id, or it could be the parent node id</param>
        /// <returns></returns>
        private IEnumerable<EditorDataItem> GetEditorDataItems(int contextId, string typeahead = null)
        {
            List<EditorDataItem> editorDataItems = new List<EditorDataItem>();

            object dotNetDataSource = AppDomain.CurrentDomain.CreateInstanceAndUnwrap(Helper.GetAssembly(this.AssemblyName).FullName, this.ClassName);

            if (dotNetDataSource != null)
            {
                if (dotNetDataSource is IDotNetDataSourceTypeahead)
                {
                    ((IDotNetDataSourceTypeahead)dotNetDataSource).Typeahead = typeahead;
                    this.handledTypeahead = true;
                }

                //if (dotNetDataSource is IDotNetDataSourcePaged)
                //{
                //    ((IDotNetDataSourcePaged)dotNetDataSource).Skip = 0;
                //    ((IDotNetDataSourcePaged)dotNetDataSource).Take = 0;
                //}

                foreach (PropertyInfo propertyInfo in dotNetDataSource.GetType().GetProperties().Where(x => this.Properties.Select(y => y.Name).Contains(x.Name)))
                {
                    if (propertyInfo.PropertyType == typeof(string))
                    {
                        string propertyValue = this.Properties.Where(x => x.Name == propertyInfo.Name).Single().Value;

                        if (propertyValue != null)
                        {
                            // process any tokens
                            propertyValue = propertyValue.Replace("$(ContextId)", contextId.ToString());

                            propertyInfo.SetValue(dotNetDataSource, propertyValue);
                        }
                    }
                    else
                    {
                        LogHelper.Warn(typeof(DotNetDataSource), "Unexpected PropertyType, " + propertyInfo.Name + " is not a string");
                    }
                }

                editorDataItems = ((IDotNetDataSource)dotNetDataSource)
                                            .GetEditorDataItems(contextId)
                                            .Select(x => new EditorDataItem() { Key = x.Key, Label = x.Value })
                                            .ToList();
            }

            return editorDataItems;
        }
    }
}