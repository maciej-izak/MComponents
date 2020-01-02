﻿using MComponents.MForm;
using MComponents.Shared.Attributes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MComponents.MGrid
{
    public class MGrid<T> : ComponentBase, IMGrid<T>
    {
        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        [Parameter]
        public RenderFragment ChildContent { get; set; }

        [Parameter]
        public Func<T> ModelFactory { get; set; }

        [Parameter]
        public IEnumerable<T> DataSource { get; set; }

        [Parameter]
        public IMGridObjectFormatter<T> Formatter { get; set; }

        [Parameter]
        public bool EnableAdding { get; set; }

        [Parameter]
        public bool EnableEditing { get; set; }

        [Parameter]
        public bool EnableDeleting { get; set; }

        [Parameter]
        public bool EnableUserSorting { get; set; }

        [Parameter]
        public bool EnableFilterRow { get; set; }

        [Parameter]
        public bool EnableExport { get; set; }

        [Parameter]
        public ToolbarItem ToolbarItems { get; set; }

        [Parameter]
        public MGridInitialState InitialState { get; set; }

        [Inject]
        public IJSRuntime JsRuntime { get; set; }


        protected List<SortInstruction> SortInstructions { get; set; } = new List<SortInstruction>();
        protected List<FilterInstruction> FilterInstructions { get; set; } = new List<FilterInstruction>();

        protected Dictionary<IMGridPropertyColumn, IMPropertyInfo> mPropertyInfoCache = new Dictionary<IMGridPropertyColumn, IMPropertyInfo>();

        public MGridPager Pager { get; set; }
        public MGridEvents<T> Events { get; set; }

        public List<IMGridColumn> ColumnsList { get; set; } = new List<IMGridColumn>();

        public Guid? Selected;

        public Guid? EditRow;
        public MForm<T> EditForm;

        public bool IsFilterRowVisible { get; protected set; }

        protected EditContext EditContext;

        protected T EditValue;

        protected T NewValue;

        protected ICollection<T> DataCache;
        protected long DataCountCache;
        protected long TotalDataCountCache;

        protected object mFilterModel;

        protected ElementReference mTableReference;
        protected int[] mColumnsWidth;

        protected SorterBuilder<T> mSorter = new SorterBuilder<T>();
        protected FilterBuilder<T> mFilter = new FilterBuilder<T>();

        protected bool mHasActionColumn;

        protected override void OnInitialized()
        {
            base.OnInitialized();

            if (Formatter == null)
                Formatter = new MGridDefaultObjectFormatter<T>();
        }

        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            ClearDataCache();
        }

        public void RegisterColumn(IMGridColumn pColumn)
        {
            if (ColumnsList.Any(c => c.Identifier == pColumn.Identifier))
                return;

            ColumnsList.Add(pColumn);

            if (pColumn is MGridActionColumn<T>)
                mHasActionColumn = true;

            if (pColumn is IMGridPropertyColumn propc)
            {
                var iprop = ReflectionHelper.GetIMPropertyInfo(typeof(T), propc.Property, propc.PropertyType);

                propc.PropertyType = iprop.PropertyType;

                if (propc.Attributes != null)
                {
                    iprop.SetAttributes(propc.Attributes);
                }

                mPropertyInfoCache.Add(propc, iprop);

                if (pColumn is IMGridSortableColumn sc && sc.SortDirection != MSortDirection.None)
                    SortInstructions.Add(new SortInstruction()
                    {
                        Direction = sc.SortDirection,
                        PropertyInfo = iprop,
                        Index = sc.SortIndex
                    });
            }

            mFilterModel = null;
            ClearDataCache();
            StateHasChanged();
        }

        public void RegisterPagerSettings(MGridPager pPager)
        {
            Pager = pPager;
            StateHasChanged();
        }

        public void RegisterEvents(MGridEvents<T> pEvents)
        {
            Events = pEvents;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            base.BuildRenderTree(builder);

            builder.OpenRegion(this.GetHashCode());

            if (this.ChildContent != null)
            {
                RenderFragment child() =>
                        (builder2) =>
                        {
                            builder2.AddContent(56, this.ChildContent);
                        };

                builder.OpenComponent<CascadingValue<MGrid<T>>>(4);
                builder.AddAttribute(5, "Value", this);
                builder.AddAttribute(6, "ChildContent", child());
                builder.CloseComponent();
            }


            RenderFragment childMain() =>
                   (builder2) =>
                   {
                       builder2.OpenElement(7, "div");
                       builder2.AddMultipleAttributes(8, AdditionalAttributes);


                       builder2.OpenElement(8, "div");
                       builder2.AddAttribute(51, "class", "btn-toolbar");
                       builder2.AddAttribute(51, "style", "justify-content: space-between;");
                       builder2.AddAttribute(52, "role", "toolbar");

                       if (ToolbarItems != ToolbarItem.None)
                       {
                           builder2.OpenElement(53, "div");
                           builder2.AddAttribute(54, "class", "btn-group mr-2");
                           builder2.AddAttribute(55, "role", "group");

                           if (EnableAdding && ToolbarItems.HasFlag(ToolbarItem.Add))
                           {
                               builder2.OpenElement(0, "button");
                               builder2.AddAttribute(0, "class", "btn btn-primary");
                               builder2.AddAttribute(21, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, OnToolbarAdd));
                               builder2.AddContent(0, (MarkupString)"<i class=\"fa fa-plus\"></i> Hinzufügen");
                               builder2.CloseElement(); // button
                           }

                           if (EnableEditing && ToolbarItems.HasFlag(ToolbarItem.Edit))
                           {
                               builder2.OpenElement(0, "button");
                               builder2.AddAttribute(0, "class", "btn btn-primary");
                               builder2.AddAttribute(21, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, OnToolbarEdit));
                               builder2.AddContent(0, (MarkupString)"<i class=\"fa fa-edit\"></i> Bearbeiten");
                               builder2.CloseElement(); // button
                           }

                           if (EnableDeleting && ToolbarItems.HasFlag(ToolbarItem.Delete))
                           {
                               builder2.OpenElement(0, "button");
                               builder2.AddAttribute(0, "class", "btn btn-primary");
                               builder2.AddAttribute(21, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, OnToolbarRemove));
                               builder2.AddContent(0, (MarkupString)"<i class=\"fa fa-trash-alt\"></i> Löschen");
                               builder2.CloseElement(); // button
                           }

                           builder2.CloseElement(); // div
                       }

                       if (EnableFilterRow)
                       {
                           builder2.OpenElement(9, "button");
                           builder2.AddAttribute(10, "class", "btn btn-primary btn-sm");
                           builder2.AddAttribute(21, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, OnToggleFilter));
                           builder2.OpenElement(9, "i");
                           builder2.AddAttribute(10, "class", "fas fa-filter");
                           builder2.CloseElement(); //i
                           builder2.AddContent(12, "Filter");
                           builder2.CloseElement(); //button
                       }


                       builder2.CloseElement(); // div


                       builder2.OpenElement(9, "table");

                       builder2.AddAttribute(10, "class", "table table-striped table-bordered table-hover table-checkable dataTable no-footer dtr-inline" + (EnableEditing ? " clickable" : string.Empty));

                       string cssClass = "margin: 15px 0px !important;";

                       builder2.AddAttribute(9, "style", cssClass);

                       builder2.AddElementReferenceCapture(27, (__value) =>
                       {
                           mTableReference = __value;
                       });

                       builder2.OpenElement(11, "thead");
                       builder2.OpenElement(12, "tr");

                       for (int i = 0; i < ColumnsList.Count; i++)
                       {
                           IMGridColumn column = ColumnsList[i];

                           if (!column.ShouldRenderColumn)
                               continue;

                           builder2.OpenElement(13, "th");
                           builder2.AddMultipleAttributes(24, column.AdditionalAttributes);

                           builder2.AddAttribute(21, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, (a) => OnColumnHeaderClick(column, a)));
                           builder2.AddAttribute(14, "scope", "col");


                           if (EditRow != null)
                           {
                               int? width = GetColumnWidth(i);
                               if (width != null)
                                   builder2.AddAttribute(8, "style", $"width: calc({width}px - (2 * (0.75rem)) - 1px);");
                           }


                           builder2.AddContent(15, (MarkupString)column.HeaderText);

                           if (EnableUserSorting)
                           {
                               var sortInstr = SortInstructions.FirstOrDefault(si => si.PropertyInfo.GetFullName() == column.Identifier);
                               if (sortInstr != null)
                               {
                                   if (sortInstr.Direction == MSortDirection.Ascending)
                                       builder2.AddContent(15, (MarkupString)"<i class=\"fa fa-arrow-down table-header-icon\"></i>");

                                   if (sortInstr.Direction == MSortDirection.Descending)
                                       builder2.AddContent(15, (MarkupString)"<i class=\"fa fa-arrow-up table-header-icon\"></i>");
                               }
                           }

                           builder2.CloseElement(); //th
                       }

                       builder2.CloseElement(); //tr
                       builder2.CloseElement(); //thead

                       builder2.AddMarkupContent(16, "\r\n");
                       builder2.OpenElement(17, "tbody");

                       if (DataCache == null)
                       {
                           IQueryable<T> data = DataSource as IQueryable<T>;

                           if (data == null)
                               data = DataSource.AsQueryable();

                           TotalDataCountCache = data.LongCount();

                           if (FilterInstructions.Count > 0)
                           {
                               data = mFilter.FilterBy(data, FilterInstructions);
                           }

                           DataCountCache = data.LongCount();

                           if (SortInstructions.Count > 0)
                           {
                               data = mSorter.SortBy(data, SortInstructions);
                           }

                           if (Pager != null)
                           {
                               data = data.Skip(Pager.PageSize * (Pager.CurrentPage - 1)).Take(Pager.PageSize);
                           }

                           DataCache = data.ToArray();
                       }

                       if (IsFilterRowVisible)
                       {
                           AddFilterRow(builder2);
                       }

                       foreach (var entry in DataCache)
                       {
                           AddContentRow(builder2, entry);
                       }

                       if (NewValue != null)
                           AddContentRow(builder2, NewValue);

                       builder2.AddMarkupContent(24, "\r\n");
                       builder2.CloseElement(); //tbody

                       builder2.CloseElement(); // table
                       builder2.AddMarkupContent(25, "\r\n");

                       if (Pager != null)
                       {
                           int pagecount = (int)Math.Ceiling(DataCountCache / (double)Pager.PageSize);

                           builder2.OpenComponent<MPager>(11);
                           builder2.AddAttribute(12, "CurrentPage", Pager.CurrentPage);
                           builder2.AddAttribute(12, "PageCount", pagecount);
                           builder2.AddAttribute(5, "OnPageChanged", EventCallback.Factory.Create<int>(this, OnPagerPageChanged));

                           builder2.AddAttribute(15, "ChildContent", (RenderFragment)((builder3) =>
                           {
                               builder3.AddMarkupContent(16, "\r\n\r\n    ");
                               builder3.OpenElement(17, "div");
                               builder3.AddAttribute(18, "class", "kt-pagination__toolbar");
                               builder3.AddMarkupContent(19, "\r\n        ");

                               if (Pager.SelectablePageSizes != null)
                               {
                                   builder3.OpenElement(20, "select");
                                   builder3.AddAttribute(21, "class", "form-control kt-font-brand");
                                   builder3.AddAttribute(22, "style", "width: 60px;");

                                   builder3.AddAttribute(23, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, OnPageSizeChange));

                                   foreach (var entry in Pager.SelectablePageSizes)
                                   {
                                       builder3.OpenElement(40, "option");
                                       builder3.AddAttribute(41, "value", entry);
                                       builder3.AddContent(42, entry);
                                       builder3.CloseElement();
                                   }

                                   builder3.AddMarkupContent(43, "\r\n");
                                   builder3.CloseElement(); //select
                               }

                               builder3.AddMarkupContent(44, "\r\n");

                               builder3.AddMarkupContent(45, $"<span class=\"pagination__desc\">{DataCache?.Count} Einträge aus {TotalDataCountCache}</span>");
                               builder3.CloseElement(); //div
                           }
                           ));

                           builder2.CloseComponent();
                       }


                       builder2.OpenElement(0, "div");
                       builder2.AddAttribute(0, "style", "padding-top: 10px;");

                       if (EnableExport)
                       {
                           builder2.OpenElement(0, "button");
                           builder2.AddAttribute(0, "class", "btn btn-secondary btn-icon btn-sm");
                           builder2.AddAttribute(0, "style", "float: right;");

                           builder2.AddAttribute(21, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, OnExportClicked));
                           builder2.AddContent(0, (MarkupString)"<i class=\"fas fa-download\"></i>");
                           builder2.CloseElement(); // button
                       }

                       builder2.CloseElement();


                       builder2.CloseElement(); //div 
                   };

            builder.OpenComponent<CascadingValue<object>>(4);
            builder.AddAttribute(5, "Value", "");
            builder.AddAttribute(6, "ChildContent", childMain());
            builder.CloseComponent();

            builder.CloseRegion();
        }

        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);

            if (firstRender)
            {
                if (InitialState == MGridInitialState.AddNewRow)
                {
                    _ = StartAdd(false);
                }
            }
        }

        private void AddFilterRow(RenderTreeBuilder pBuilder)
        {
            if (mFilterModel == null)
            {
                var fmodel = new ExpandoObject() as IDictionary<string, object>;

                foreach (var column in ColumnsList)
                {
                    if (column is IMGridPropertyColumn pc)
                        fmodel.Add(pc.Property, null);
                }

                mFilterModel = fmodel;
            }

            pBuilder.OpenElement(19, "tr");
            pBuilder.AddAttribute(20, "class", "mgrid-row mgrid-edit-row");

            pBuilder.OpenComponent<MForm<T>>(53);
            pBuilder.AddAttribute(54, "Model", mFilterModel);
            pBuilder.AddAttribute(55, "IsInTableRow", true);
            pBuilder.AddAttribute(55, "EnableValidation", false);
            pBuilder.AddAttribute(55, "data-is-filterrow", true);

            pBuilder.AddAttribute(23, "OnValueChanged", EventCallback.Factory.Create<MFormValueChangedArgs>(this, OnFilterValueChanged));

            pBuilder.AddAttribute(56, "Fields", (RenderFragment)((builder3) =>
            {
                bool columnFixedSize = !(mHasActionColumn && !EnableEditing && !EnableDeleting);

                for (int i = 0; i < ColumnsList.Count; i++)
                {
                    IMGridColumn column = ColumnsList[i];
                    int? size = columnFixedSize ? GetColumnWidth(i) : null;
                    AddMFormField(builder3, column, true, size);
                }
            }));

            pBuilder.CloseComponent();

            pBuilder.CloseElement(); //tr;

        }


        private void AddContentRow(RenderTreeBuilder builder2, T entry)
        {
            Guid entryId = GetId(entry);

            bool rowEdit = EditRow.HasValue && EditRow.Value == entryId;

            builder2.AddMarkupContent(18, "\r\n");
            builder2.OpenElement(19, "tr");

            string cssClass = "mgrid-row";

            if (rowEdit)
            {
                cssClass += " mgrid-edit-row";
            }

            bool selected = Selected.HasValue && Selected == entryId;
            if (selected)
                cssClass += " highlight";

            Formatter.AppendToTableRow(builder2, ref cssClass, entry, selected);

            builder2.AddAttribute(20, "class", cssClass);


            builder2.AddAttribute(21, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, (a) =>
            {
                OnRowClick(entry);
            }));

            builder2.AddEventStopPropagationAttribute(4, "onclick", true);


            builder2.AddAttribute(21, "ondblclick", EventCallback.Factory.Create<MouseEventArgs>(this, async (a) =>
            {
                await StartEditRow(entry);
            }));


            if (rowEdit)
            {
                EditValue = entry;

                builder2.OpenElement(50, "td");
                builder2.AddAttribute(51, "colspan", ColumnsList.Count);

                builder2.OpenElement(50, "div");

                builder2.OpenElement(50, "table");
                builder2.AddAttribute(32, "style", "table-layout: fixed;");

                builder2.OpenElement(50, "tbody");
                builder2.OpenElement(50, "tr");

                {

                    builder2.OpenComponent<MForm<T>>(53);
                    builder2.AddAttribute(54, "Model", EditValue);
                    builder2.AddAttribute(55, "IsInTableRow", true);
                    builder2.AddAttribute(23, "OnValidSubmit", EventCallback.Factory.Create<MFormSubmitArgs>(this, async (a) =>
                    {
                        await OnFormSubmit(a);
                    }));
                    builder2.AddAttribute(23, "OnValueChanged", EventCallback.Factory.Create<MFormValueChangedArgs>(this, OnEditValueChanged));
                    builder2.AddAttribute(56, "Fields", (RenderFragment)((builder3) =>
                    {
                        for (int i = 0; i < ColumnsList.Count; i++)
                        {
                            IMGridColumn column = ColumnsList[i];
                            if (!column.ShouldRenderColumn)
                                continue;
                            AddMFormField(builder3, column, false, GetColumnWidth(i));
                        }
                    }));

                    builder2.AddComponentReferenceCapture(12, (__value) =>
                    {
                        EditForm = (MForm<T>)__value;
                    });

                    builder2.CloseComponent();
                }

                builder2.CloseElement(); //tr
                builder2.CloseElement(); //tbody
                builder2.CloseElement(); //table

                builder2.CloseElement(); //div

                builder2.CloseElement(); //td  

            }
            else
            {
                foreach (var column in ColumnsList)
                {
                    if (!column.ShouldRenderColumn)
                        continue;

                    builder2.OpenElement(22, "td");

                    Formatter.AppendToTableRowData(builder2, column, entry);

                    if (column is IMGridColumnGenerator<T> generator)
                    {
                        builder2.AddContent(25, generator.GenerateContent(entry));
                    }
                    else if (column is IMGridPropertyColumn pc)
                    {
                        var iprop = mPropertyInfoCache[pc];
                        builder2.AddContent(25, Formatter.FormatPropertyColumnValue(pc, iprop, entry));
                    }

                    builder2.CloseElement(); //td
                }
            }

            builder2.CloseElement(); //tr
        }

        private void AddMFormField(RenderTreeBuilder builder3, IMGridColumn column, bool pIsInFilterRow, int? pSize)
        {
            if (column is IMGridPropertyColumn pc)
            {
                var propertyType = mPropertyInfoCache[pc].PropertyType;

                if (pIsInFilterRow)
                    propertyType = GetNullableTypeIfNeeded(propertyType);

                var method = typeof(MGrid<T>).GetMethod(nameof(MGrid<T>.AddPropertyField), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(propertyType);
                method.Invoke(this, new object[] { builder3, column, pc, pIsInFilterRow, pSize });

                //  AddPropertyField(builder3, column, pc, propertyType);
            }
            else if (column is IMGridEditFieldGenerator<T> fieldGenerator)
            {
                AddFieldGenerator(builder3, fieldGenerator, pSize);
            }
            else
            {
                builder3.OpenComponent<MField>(5);
                builder3.CloseComponent();
            }
        }

        private static Type GetNullableTypeIfNeeded(Type propertyType)
        {
            if (propertyType.IsValueType && (!propertyType.IsGenericType || propertyType.GetGenericTypeDefinition() != typeof(Nullable<>)))
            {
                propertyType = typeof(Nullable<>).MakeGenericType(propertyType);
            }

            return propertyType;
        }

        private void AddPropertyField<TProperty>(RenderTreeBuilder builder3, IMGridColumn column, IMGridPropertyColumn pc, bool pIsInFilterRow, int? pSize)
        {
            var attributes = mPropertyInfoCache[pc].GetAttributes()?.ToList() ?? new List<Attribute>();

            if (pIsInFilterRow)
            {
                if (!column.EnableFilter && !attributes.OfType<ReadOnlyAttribute>().Any())
                {
                    attributes.Add(new ReadOnlyAttribute());
                }
                if (column.EnableFilter && attributes.Any(a => a is ReadOnlyAttribute))
                {
                    attributes = attributes.Where(a => !(a is ReadOnlyAttribute)).ToList();
                }
            }

            if (column is IMComplexColumn<TProperty> complex)
            {
                builder3.OpenComponent<MComplexPropertyField<TProperty>>(5);

                builder3.AddAttribute(6, "Property", pc.Property);
                builder3.AddAttribute(7, "PropertyType", typeof(TProperty));
                builder3.AddAttribute(8, "Attributes", attributes.ToArray());

                if (!pIsInFilterRow || (pIsInFilterRow && column.EnableFilter))
                    builder3.AddAttribute(23, "Template", complex.FormTemplate);

                if (pSize != null)
                    builder3.AddAttribute(8, "style", $"width: {pSize - 1}px;");

                builder3.CloseComponent();
            }
            else
            {
                builder3.OpenComponent<MField>(5);

                builder3.AddAttribute(6, "Property", pc.Property);
                builder3.AddAttribute(7, "PropertyType", typeof(TProperty));
                builder3.AddAttribute(8, "Attributes", attributes.ToArray());

                if (pSize != null)
                    builder3.AddAttribute(8, "style", $"width: {pSize - 1}px;");

                builder3.CloseComponent();
            }
        }

        private static void AddFieldGenerator(RenderTreeBuilder builder3, IMGridEditFieldGenerator<T> pFieldGenerator, int? pSize)
        {
            builder3.OpenComponent<MFieldGenerator<T>>(5);
            builder3.AddAttribute(23, "Template", pFieldGenerator.Template(pSize));

            if (pSize != null)
                builder3.AddAttribute(8, "style", $"width: {pSize - 1}px;");

            builder3.CloseComponent();
        }


        protected async void OnRowClick(T pValue)
        {
            Guid id = GetId(pValue);

            if (id == EditRow)
                return;

            if (id == Selected)
            {
                if (EditRow != null && EditRow != Selected)
                {
                    await StopEditing(true, true);
                    await StartEdit(id, true);
                    StateHasChanged();
                }
                else if (EditRow == null)
                {
                    await StartEdit(id, true);
                }
                return;
            }

            if (EditRow != null)
            {
                await StopEditing(true, true);
            }

            Selected = id;
            StateHasChanged();
        }

        protected async Task StopEditing(bool pInvokeSubmit, bool pUserInteracted)
        {
            if (pInvokeSubmit)
            {
                //Currently not sure if this makes sense. This should move the focus from an open input element which triggers to save the value
                //right now it's unknown if this workaround is needed or if it works anyway
                await JsRuntime.InvokeVoidAsync("mcomponents.focusElement", mTableReference);
                await Task.Delay(10);
                EditForm?.CallLocalSubmit(pUserInteracted);
            }

            EditForm = null;
            NewValue = default(T);
            EditRow = null;

            StateHasChanged();
        }

        public async Task StartEditRow(T pValue)
        {
            Guid id = GetId(pValue);

            if (id == EditRow)
                return;

            await StartEdit(id, true);
        }

        private async Task StartEdit(Guid? id, bool pUserInteracted)
        {
            await StopEditing(true, pUserInteracted);

            if (!EnableEditing)
                return;

            Guid? toSelect = id ?? Selected;

            if (toSelect == null)
                return;

            if (Events?.OnBeginEdit != null)
            {
                var args = new BeginEditArgs<T>()
                {
                    Row = GetDataFromId(toSelect)
                };

                await Events.OnBeginEdit.InvokeAsync(args);

                if (args.Cancelled)
                    return;
            }

            await UpdateColumnsWidth();

            EditRow = toSelect;
            Selected = EditRow;
            StateHasChanged();
        }

        private async Task UpdateColumnsWidth()
        {
            mColumnsWidth = await JsRuntime.InvokeAsync<int[]>("mcomponents.getColumnsWith", new object[] { mTableReference });
        }

        public async Task StartAdd(bool pUserInteracted)
        {
            await StopEditing(true, pUserInteracted);

            var newv = CreateNewT();

            if (Events?.OnBeginAdd != null)
            {
                var args = new BeginAddArgs<T>()
                {
                    Row = newv
                };

                await Events.OnBeginAdd.InvokeAsync(args);

                if (args.Cancelled)
                    return;
            }

            EditRow = GetId(newv);
            Selected = EditRow;

            NewValue = newv;

            await UpdateColumnsWidth();

            StateHasChanged();
        }

        protected async void OnToolbarAdd()
        {
            await StartAdd(true);
        }

        protected async void OnToolbarEdit()
        {
            if (EditRow != null)
            {
                await StopEditing(true, true);
                StateHasChanged();
                return;
            }

            await StartEdit(null, true);
        }

        protected async void OnToolbarRemove()
        {
            await StopEditing(true, true);

            if (Selected == null)
                return;

            T value = GetDataFromId(Selected);

            await StartDeleteRow(value);
        }

        protected async void OnToggleFilter()
        {
            IsFilterRowVisible = !IsFilterRowVisible;

            if (IsFilterRowVisible)
            {
                await UpdateColumnsWidth();
            }
            else
            {
                ClearFilterValues();
            }

            StateHasChanged();
        }

        public async Task StartDeleteRow(T value)
        {
            if (!EnableDeleting)
                return;

            if (Events?.OnBeginDelete != null)
            {
                var args = new BeginDeleteArgs<T>()
                {
                    Row = value
                };

                await Events.OnBeginDelete.InvokeAsync(args);

                if (args.Cancelled)
                    return;
            }

            if (DataSource is ICollection<T> coll)
                coll.Remove(value);

            if (Events?.OnAfterDelete != null)
            {
                await Events.OnAfterDelete.InvokeAsync(new AfterDeleteArgs<T>()
                {
                    Row = value
                });
            }

            Selected = null;
            ClearDataCache();

            StateHasChanged();
        }

        private T GetDataFromId(Guid? pId)
        {
            if (!pId.HasValue)
                return default;

            if (DataCache == null)
                return default;

            return DataCache.FirstOrDefault(d => GetId(d) == pId);
        }

        protected async Task OnFormSubmit(MFormSubmitArgs args)
        {
            if (args.ChangedValues.Count > 0 || args.UserInteracted)
            {
                if (NewValue == null)
                {
                    T value = (T)args.Model;

                    if (DataSource is Collection<T> ds) //update obversable collection
                    {
                        int index = ds.IndexOf(value);
                        ds[index] = value;
                    }

                    if (Events?.OnAfterEdit != null)
                    {
                        await Events.OnAfterEdit.InvokeAsync(new AfterEditArgs<T>()
                        {
                            Row = value
                        });
                    }
                }
                else
                {
                    if (DataSource is ICollection<T> coll)
                        coll.Add(NewValue);

                    if (Events?.OnAfterAdd != null)
                    {
                        await Events.OnAfterAdd.InvokeAsync(new AfterAddArgs<T>()
                        {
                            Row = NewValue
                        });
                    }
                }

                ClearDataCache();
            }

            await StopEditing(false, false);
        }

        protected void OnPagerPageChanged(int pPage)
        {
#pragma warning disable BL0005 // Component parameter should not be set outside of its component.
            Pager.CurrentPage = pPage;
#pragma warning restore BL0005 // Component parameter should not be set outside of its component.

            ClearDataCache();
            StateHasChanged();
        }

        protected void OnPageSizeChange(ChangeEventArgs pValue)
        {
#pragma warning disable BL0005 // Component parameter should not be set outside of its component.
            Pager.PageSize = int.Parse(pValue.Value.ToString());
            Pager.CurrentPage = 1;
#pragma warning restore BL0005 // Component parameter should not be set outside of its component.

            ClearDataCache();
            StateHasChanged();
        }


        protected void OnColumnHeaderClick(IMGridColumn pColumn, MouseEventArgs pArgs)
        {
            if (!EnableUserSorting)
                return;

            if (!(pColumn is IMGridSortableColumn sc) || !sc.EnableSort)
                return;

            var propInfoColumn = pColumn as IMGridPropertyColumn;

            if (propInfoColumn == null)
                return; // other columns not supported yet

            var instr = SortInstructions.FirstOrDefault(s => s.PropertyInfo.GetFullName() == pColumn.Identifier);

            if (instr == null)
            {
                if (!pArgs.ShiftKey)
                    SortInstructions.Clear();

                var propInfo = mPropertyInfoCache[propInfoColumn];

                SortInstructions.Add(new SortInstruction()
                {
                    Direction = MSortDirection.Ascending,
                    PropertyInfo = propInfo,
                    Index = SortInstructions.Count
                });
            }
            else if (instr.Direction == MSortDirection.Ascending)
            {
                instr.Direction = MSortDirection.Descending;
            }
            else if (instr.Direction == MSortDirection.Descending)
            {
                SortInstructions.Remove(instr);
            }

            ClearDataCache();
            StateHasChanged();
        }

        protected async void OnEditValueChanged(MFormValueChangedArgs pArgs)
        {
            if (Events?.OnFormValueChanged != null)
            {
                await Events.OnFormValueChanged.InvokeAsync(pArgs);
            }
        }

        protected void OnFilterValueChanged(MFormValueChangedArgs pArgs)
        {
            var iprop = mPropertyInfoCache.First(v => v.Key.Property == pArgs.Property).Value;

            FilterInstructions.RemoveAll(f => f.PropertyInfo == iprop);

            if (pArgs.NewValue != null && !(pArgs.NewValue is string a && a == string.Empty)) //all values from filter are nullable
            {
                FilterInstructions.Add(new FilterInstruction()
                {
                    PropertyInfo = iprop,
                    Value = pArgs.NewValue
                });
            }

#pragma warning disable BL0005 // Component parameter should not be set outside of its component.
            if (Pager != null)
                Pager.CurrentPage = 1;
#pragma warning restore BL0005 // Component parameter should not be set outside of its component.

            ClearDataCache();
            StateHasChanged();
        }

        protected async void OnExportClicked()
        {
            var data = ExcelExportHelper.GetExcelSpreadsheet<T>(ColumnsList, mPropertyInfoCache, DataSource, Formatter);
            await FileUtil.SaveAs(JsRuntime, "Export.xlsx", data);
        }

        public Guid GetId(T pItem)
        {
            return (Guid)ReflectionHelper.GetPropertyValue(pItem, "Id");
        }

        protected T CreateNewT()
        {
            if (ModelFactory != null)
            {
                return ModelFactory();
            }

            return (T)Activator.CreateInstance(typeof(T));
        }

        protected void ClearDataCache()
        {
            DataCache = null;
            DataCountCache = -1;
            TotalDataCountCache = -1;
        }

        public void Refresh()
        {
            ClearDataCache();
            StateHasChanged();
        }

        public void ClearColumns()
        {
            ColumnsList.Clear();
            mPropertyInfoCache.Clear();
            ClearFilterValues();
            Refresh();
        }

        public void InvokeStateHasChanged()
        {
            InvokeAsync(StateHasChanged);
        }

        public async Task SavePendingChanges(bool pUserInteracted)
        {
            await StopEditing(true, pUserInteracted);
        }

        protected int? GetColumnWidth(int pIndex)
        {
            if (mColumnsWidth == null)
                return null;

            if (pIndex < mColumnsWidth.Length)
            {
                return mColumnsWidth[pIndex];
            }

            return null;
        }

        public void ClearFilterValues()
        {
            FilterInstructions.Clear();
            mFilterModel = null;
            Refresh();
        }

        public bool IsEditingRow => EditRow != null;

    }
}