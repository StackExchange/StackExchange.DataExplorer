using System;
using System.Collections.Generic;
using System.Linq;

namespace StackExchange.DataExplorer.Helpers
{
    public interface IPagedList
    {
        int TotalCount { get; set; }
        int PageCount { get; set; }
        int PageIndex { get; set; }
        int PageSize { get; set; }
    }

    public class PagedList<T> : List<T>, IPagedList
    {
        public PagedList() { }

        // we need a distinct overload for IQueryable, so efficient paging queries can be created by linq2sql
        public PagedList(IQueryable<T> source, int index, int pageSize)
        {
            TotalCount = source.Count();
            PageSize = pageSize;
            SetPageIndex(index);
            PageCount = GetPageCount();

            AddRange(source.Skip(PageIndex * PageSize).Take(PageSize).ToList());
        }

        public PagedList(IEnumerable<T> source, int index, int pageSize, bool forceIndexInBounds = false, int? prePagedTotalCount = null)
        {
            TotalCount = prePagedTotalCount ?? source.Count();
            PageSize = pageSize;

            // viewing outside the bounds
            if (forceIndexInBounds && TotalCount > 0 && index * PageSize > TotalCount)
            {
                // so let them view the last page
                index = GetPageCount();
            }

            SetPageIndex(index);
            PageCount = GetPageCount();

            if (prePagedTotalCount.HasValue)
            {
                AddRange(source);
            }
            else
            {
                AddRange(source.Skip(PageIndex * PageSize).Take(PageSize));
            }
        }

        private void SetPageIndex(int index)
        {
            // our urls assume 1-based indexing into pages; convert to 0-based and don't allow negatives
            PageIndex = index < 1 ? 0 : index - 1;
        }

        /// <summary>
        /// Should be called *after* TotalCount and PageSize have been initialized.
        /// </summary>
        private int GetPageCount()
        {
            if (PageSize == 0) // you get what you ask for
                return 0;

            var remainder = TotalCount % PageSize;
            return (TotalCount / PageSize) + (remainder == 0 ? 0 : 1); // only need another page if we have spillover
        }

        public IEnumerable<IEnumerable<T>> ToRows(int totalRows)
        {
            return this.Select((item, index) => new {Item = item, Index = index})
                .GroupBy(o => o.Index / (PageSize / totalRows))
                .Select(g => g.Select(o => o.Item));
        }

        /// <summary>
        /// For API use - takes an already paged list with a known total count; doesn't do further paging operations.
        /// </summary>
        /// <remarks>
        /// Allows this new PagedList to store the paging properties of a previous PagedList without calling Convert.
        /// We need this when paging on the cached lists of post ids and converting them to Api Questions.
        /// This is such an awful hack.
        /// </remarks>
        public PagedList(IEnumerable<T> pagedSource, IPagedList other)
        {
            TotalCount = other.TotalCount;
            PageSize = other.PageSize;
            PageCount = other.PageCount;
            PageIndex = other.PageIndex;
            AddRange(pagedSource);
        }

        public int TotalCount { get; set; }
        public int PageCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }

        public bool IsPreviousPage => PageIndex > 0;
        public bool IsNextPage => PageIndex * PageSize <= TotalCount;


        /// <summary>
        /// Returns a new PagedList of <typeparamref name="TNew"/> that is created by calling <paramref name="converter"/> on
        /// each element within this list's paging window.
        /// </summary>
        /// <typeparam name="TNew">The type that the resulting PagedList will be of.</typeparam>
        /// <param name="converter">Function that will return the replacement for each item in this PagedList, usually a hydration via DBContext.</param>
        public PagedList<TNew> Convert<TNew>(Func<T, TNew> converter)
        {
            // retain all the paging properties
            var result = new PagedList<TNew>
            {
                TotalCount = TotalCount,
                PageCount = PageCount,
                PageIndex = PageIndex,
                PageSize = PageSize
            };

            // call the converter to generate the data we want in the resulting list
            foreach (var item in this)
            {
                result.Add(converter(item));
            }

            return result;
        }
    }
}