using System;
using System.Collections.Generic;

namespace Swihoni.Components
{
    public enum Navigation
    {
        Continue,
        SkipDescendents, // Skip all children of current node
        Exit             // Exit entire tree immediately
    }

    public static class ElementExtensions
    {
        #region Anti-allocation measures for closures

        private static bool _areEqual;

        private interface IVisitFunc
        {
            Navigation Invoke(in TriArray<ElementBase> _zip);

            bool CanInvoke();
        }

        private struct VisitPropsAction : IVisitFunc
        {
            internal Action<PropertyBase> action;

            public Navigation Invoke(in TriArray<ElementBase> _zip)
            {
                if (_zip[0] is PropertyBase property) action(property);
                return Navigation.Continue;
            }

            public bool CanInvoke() => action != null;
        }

        private struct VisitFunc : IVisitFunc
        {
            internal Func<ElementBase, Navigation> function;
            public Navigation Invoke(in TriArray<ElementBase> _zip) => function.Invoke(_zip[0]);
            public bool CanInvoke() => function != null;
        }

        private struct DualVisitFunc : IVisitFunc
        {
            internal Func<ElementBase, ElementBase, Navigation> function;
            public Navigation Invoke(in TriArray<ElementBase> _zip) => function(_zip[0], _zip[1]);
            public bool CanInvoke() => function != null;
        }

        private struct TriVisitFunc : IVisitFunc
        {
            internal Func<ElementBase, ElementBase, ElementBase, Navigation> function;
            public Navigation Invoke(in TriArray<ElementBase> _zip) => function(_zip[0], _zip[1], _zip[2]);
            public bool CanInvoke() => function != null;
        }

        #endregion

        public static bool IsElement(this Type type) => type.IsSubclassOf(typeof(ElementBase));

        public static void AppendAll<T>(this List<T> enumerable, params T[] elements) => enumerable.AddRange(elements);

        /// <summary>
        /// Un-sets with value flag.
        /// If you instead want to zero, see <see cref="Zero{T}"/>
        /// </summary>
        public static ElementBase Clear(this ElementBase element) => element.NavigateProperties(_p => _p.Clear());

        /// <summary>
        /// Reset all properties to default values.
        /// Sets with value flags as well.
        /// </summary>
        public static T Zero<T>(this T element) where T : ElementBase
        {
            element.NavigateProperties(_p => _p.Zero());
            return element;
        }

        /// <summary>
        /// Allocates a cloned instance. Do not use in loops.
        /// </summary>
        public static TElement Clone<TElement>(this TElement element) where TElement : ElementBase
        {
            var clone = (TElement)element.GetType().NewElement();
            NavigateZipped(element, clone, (_e1, _e2) =>
            {
                if (_e1 is Container p1 && _e2 is Container p2)
                    p2.TakeElementTypes(p1);
                return Navigation.Continue;
            });
            clone.MergeFrom(element);
            return clone;
        }

        /// <summary>
        /// Test if all properties are equal. Uses <see cref="PropertyBase.Equals(PropertyBase)"/> for comparision.
        /// </summary>
        public static bool EqualTo<T>(this T e1, T e2) where T : ElementBase
        {
            _areEqual = true;
            NavigateZipped(e1, e2, (_e1, _e2) =>
            {
                if (_e1 is PropertyBase p1 && _e2 is PropertyBase p2)
                {
                    if (p1.Equals(p2))
                        return Navigation.Continue;
                    _areEqual = false;
                    return Navigation.Exit;
                }
                return Navigation.Continue;
            });
            return _areEqual;
        }

        /// <summary>See: <see cref="Navigate"/></summary>
        public static ElementBase NavigateProperties(this ElementBase e, Action<PropertyBase> visit)
        {
            var zip = new TriArray<ElementBase> { [0] = e };
            Navigate(new VisitPropsAction { action = visit }, zip, 1);
            return e;
        }

        /// <summary>See: <see cref="Navigate"/></summary>
        public static ElementBase Navigate(this ElementBase e, Func<ElementBase, Navigation> visit, Func<ElementBase, Navigation> endVisit = null)
        {
            var zip = new TriArray<ElementBase> { [0] = e };
            Navigate(new VisitFunc { function = visit }, zip, 1, new VisitFunc { function = endVisit });
            return e;
        }

        /// <summary>See: <see cref="Navigate"/></summary>
        public static void NavigateZipped(ElementBase e1, ElementBase e2, Func<ElementBase, ElementBase, Navigation> visit,
                                          Func<ElementBase, ElementBase, Navigation> endVisit = null)
        {
            var zip = new TriArray<ElementBase> { [0] = e1, [1] = e2 };
            Navigate(new DualVisitFunc { function = visit }, zip, 2, new DualVisitFunc { function = endVisit });
        }

        /// <summary>See: <see cref="Navigate"/></summary>
        public static void NavigateZipped(ElementBase e1, ElementBase e2, ElementBase e3, Func<ElementBase, ElementBase, ElementBase, Navigation> visit,
                                          Func<ElementBase, ElementBase, ElementBase, Navigation> endVisit = null)
        {
            var zip = new TriArray<ElementBase> { [0] = e1, [1] = e2, [2] = e3 };
            Navigate(new TriVisitFunc { function = visit }, zip, 3, new TriVisitFunc { function = endVisit });
        }

        /// <summary>
        /// Iterates over all elements in a zipped fashion. Similar to the zip function in python.
        /// Important limitations: uses orders of elements for components and containers. It keeps iterates only if the next two elements are the same.
        /// This function needs to be performant since it is designed to be called in game update loop.
        /// It is templated to avoid boxing with the structs that hold lambda references.
        /// </summary>
        /// <param name="visit">Called at each node in the "tree" of elements.</param>
        /// <param name="zip">Zipped element roots.</param>
        /// <param name="size">Amount of elements zipped to zip together. Max three supported.</param>
        /// <param name="exitVisit"></param>
        /// <exception cref="ArgumentException">If an object navigated was not an element.</exception>
        private static void Navigate<TVisit>(TVisit visit, in TriArray<ElementBase> zip, int size, TVisit exitVisit = default) where TVisit : IVisitFunc
        {
            if (size <= 0) throw new ArgumentException("Size needs to be greater than zero");
            var exitAll = false;
            void NavigateRecursively(in TriArray<ElementBase> _zip)
            {
                Navigation navigation = visit.Invoke(_zip);
                if (navigation == Navigation.Exit) exitAll = true;
                if (exitAll || navigation == Navigation.SkipDescendents) return;

                switch (_zip[0])
                {
                    case Container:
                    {
                        var zippedContainers = new TriArray<Container>();
                        // Truncate iteration to count of smallest elements collection
                        var elementSize = int.MaxValue;
                        for (var i = 0; i < size; i++)
                        {
                            zippedContainers[i] = (Container)_zip[i];
                            int count = zippedContainers[i].Elements.Count;
                            if (count < elementSize) elementSize = count;
                        }
                        // Truncate iteration if elements become different types
                        var foundDifferentType = false;
                        for (var j = 0; j < elementSize && !foundDifferentType; j++)
                        {
                            Type firstType = zippedContainers[0][j].GetType();
                            for (var i = 1; i < size && !foundDifferentType; i++)
                            {
                                if (zippedContainers[i][j].GetType() == firstType) continue;
                                elementSize = j;
                                foundDifferentType = true;
                            }
                        }

                        for (var j = 0; j < elementSize; j++)
                        {
                            var zippedChildren = new TriArray<ElementBase>();
                            for (var i = 0; i < size; i++) zippedChildren[i] = zippedContainers[i][j];
                            NavigateRecursively(zippedChildren);
                        }
                        break;
                    }
                    case ArrayElementBase a1:
                    {
                        for (var j = 0; j < a1.Length; j++)
                        {
                            var zippedElements = new TriArray<ElementBase>();
                            for (var i = 0; i < size; i++) zippedElements[i] = ((ArrayElementBase)_zip[i])[j];
                            NavigateRecursively(zippedElements);
                        }
                        break;
                    }
                    case ComponentBase c1:
                    {
                        for (var j = 0; j < c1.Elements.Count; j++)
                        {
                            var zippedChildren = new TriArray<ElementBase>();
                            for (var i = 0; i < size; i++) zippedChildren[i] = ((ComponentBase)_zip[i])[j];
                            NavigateRecursively(zippedChildren);
                        }
                        break;
                    }
                    default:
                    {
                        if (_zip[0] is not PropertyBase) throw new ArgumentException("Expected component or array");
                        break;
                    }
                }
                if (exitVisit.CanInvoke()) exitVisit.Invoke(_zip);
            }
            NavigateRecursively(zip);
        }

        /// <summary>
        /// Avoids excessive heap allocation by providing a fixed size array.
        /// There is no easy way to create a fixed size array on the stack as of the current C# version for Unity.
        /// I don't like it either okay.
        /// </summary>
        private struct TriArray<T>
        {
            private T m_E1, m_E2, m_E3;

            public T this[int index]
            {
                get
                {
                    return index switch
                    {
                        0 => m_E1,
                        1 => m_E2,
                        2 => m_E3,
                        _ => throw new IndexOutOfRangeException()
                    };
                }
                set
                {
                    switch (index)
                    {
                        case 0:  m_E1 = value; break;
                        case 1:  m_E2 = value; break;
                        case 2:  m_E3 = value; break;
                        default: throw new IndexOutOfRangeException();
                    }
                }
            }
        }
    }
}