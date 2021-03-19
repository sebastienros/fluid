using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Objects
{
    public class PaginateObject
    {
        public int CurrentOffset { get; set; }
        public int CurrentPage { get; set; }
        public long Items { get; set; }
        public List<PaginatePartObject> Parts { get; set; } = new List<PaginatePartObject>();
        public PaginatePartObject Previous { get; set; }
        public PaginatePartObject Next { get; set; }
        public int PageSize { get; set; }
        public long Pages { get; set; }

        public static async Task<PaginateObject> Create(PaginationValue value, int pageSize)
        {
            var ret = new PaginateObject
            {
                Items = await value.GetItemCount(),
                PageSize = pageSize,
                CurrentPage = value.CurrentPage,
                CurrentOffset = (value.CurrentPage - 1) * pageSize
            };
            ret.Pages = ret.Items / ret.PageSize;
            if (ret.Items % ret.PageSize > 0)
            {
                ret.Pages++;
            }

            if (ret.Pages > 1)
            {
                if (ret.CurrentPage > 1)
                {
                    ret.Previous = new PaginatePartObject
                    {
                        IsLink = true,
                        Title = "« Previous",
                        Url = value.BuildUrl(ret.CurrentPage - 1)
                    };
                }
                
                if (ret.CurrentPage < ret.Pages)
                {
                    ret.Next = new PaginatePartObject
                    {
                        IsLink = true,
                        Title = "Next »",
                        Url = value.BuildUrl(ret.CurrentPage + 1)
                    };
                }

                var min = ret.CurrentPage - 2L;
                var max = ret.CurrentPage + 2L;
                if (min <= 1)
                {
                    min = 2;
                }

                if (max >= ret.Pages)
                {
                    max = ret.Pages - 1;
                }

                var last = 0;
                for (var page = 1; page <= ret.Pages; page++)
                {
                    var add = false;
                    if (page == 1)
                    {
                        add = true;
                    }
                    else if (page == ret.Pages)
                    {
                        add = true;
                    }
                    else if (page >= min && page <= max)
                    {
                        add = true;
                    }

                    if (!add)
                    {
                        continue;
                    }

                    if (last + 1 != page)
                    {
                        ret.Parts.Add(new PaginatePartObject
                        {
                            IsLink = false,
                            Title = "…"
                        });
                    }

                    last = page;
                    var item = new PaginatePartObject
                    {
                        Title = page.ToString(),
                        IsLink = page != ret.CurrentPage
                    };
                    if (item.IsLink)
                    {
                        item.Url = value.BuildUrl(page);
                    }

                    ret.Parts.Add(item);
                }
            }

            return ret;
        }
    }
}