﻿// Copyright (C) 2023  Liu, Han; School of Software, Tsinghua University
//
// This file is part of CBIMS.LDP.
// CBIMS.LDP is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// CBIMS.LDP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.
// You should have received a copy of the GNU Lesser General Public License along with CBIMS.LDP. If not, see <https://www.gnu.org/licenses/>.

using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VDS.RDF;

namespace CBIMS.LDP.Def
{


    public interface IRdfPropDef : IRdfNode
    {
        IEnumerable<IRdfClassDef> Domain { get; }
        IEnumerable<IRdfClassDef> Range { get; }
        IEnumerable<IRdfPropDef> SuperProps { get; }
    }

    public class RdfPropDef : RdfInstPersist, IRdfPropDef
    {
        public IEnumerable<IRdfClassDef> Domain => GetProp<IRdfClassDef>(RDFSCommonDef.domain.QName);
        public IEnumerable<IRdfClassDef> Range => GetProp<IRdfClassDef>(RDFSCommonDef.range.QName);
        public IEnumerable<IRdfPropDef> SuperProps => GetProp<IRdfPropDef>(RDFSCommonDef.subPropertyOf.QName);

        public bool IsLiteral => Range != null && Range.Any() && Range.All(t => t is IRdfLiteralClassDef);


        public RdfPropDef(RdfNSDef ns, string name, 
            IUriNode node = null, Action<RdfPropDef> actionOnInitialize = null) 
            : base(ns, name, RDFSCommonDef.Property, node)
        {
            if (actionOnInitialize != null)
            {
                actionOnInitialize(this);
            }
        }
        
        protected RdfPropDef(RdfNSDef ns, string name, IRdfClassDef type
            , IUriNode node, Action<RdfPropDef> actionOnInitialize) 
            : base(ns, name, type, node)
        {
            if (actionOnInitialize != null)
            {
                actionOnInitialize(this);
            }
        }

        public RdfPropDef(RdfNSDef ns, string name, IRdfURIClassDef domain, IRdfClassDef range, 
            IUriNode node = null, Action<RdfPropDef> actionOnInitialize = null) 
            : base(ns, name, RDFSCommonDef.Property, node)
        {
            SetProp(RDFSCommonDef.domain.QName, domain);
            SetProp(RDFSCommonDef.range.QName, range);
            if (actionOnInitialize != null)
            {
                actionOnInitialize(this);
            }
        }

        public RdfPropDef(RdfNSDef ns, string name, 
            IEnumerable<IRdfURIClassDef> domains, IEnumerable<IRdfClassDef> ranges, 
            IUriNode node = null, Action<RdfPropDef> actionOnInitialize = null) 
            : base(ns, name, RDFSCommonDef.Property, node)
        {
            SetProps(RDFSCommonDef.domain.QName, domains);
            SetProps(RDFSCommonDef.range.QName, ranges);
            if (actionOnInitialize != null)
            {
                actionOnInitialize(this);
            }
        }



        public void AddDomain(IRdfClassDef def)
        {
            AddProp(RDFSCommonDef.domain.QName, def);
        }
        public void AddRange(IRdfClassDef def)
        {
            AddProp(RDFSCommonDef.range.QName, def);
        }
        public void AddSuperProp(IRdfPropDef def)
        {
            AddProp(RDFSCommonDef.subPropertyOf.QName, def);
        }
    }

    public class RdfStringPropDef : RdfPropDef
    {
        public RdfStringPropDef(RdfNSDef ns, string name, IRdfURIClassDef domain, 
            IUriNode node = null, Action<RdfPropDef> actionOnInitialize = null) 
            : base(ns, name, domain, XsdLiteral.STRING, node, actionOnInitialize)
        {
        }
    }
    public class RdfIntegerPropDef : RdfPropDef
    {
        public RdfIntegerPropDef(RdfNSDef ns, string name, IRdfURIClassDef domain,
            IUriNode node = null, Action<RdfPropDef> actionOnInitialize = null) 
            : base(ns, name, domain, XsdLiteral.INTEGER, node, actionOnInitialize)
        {
        }
    }
    public class RdfDoublePropDef : RdfPropDef
    {
        public RdfDoublePropDef(RdfNSDef ns, string name, IRdfURIClassDef domain, 
            IUriNode node = null, Action<RdfPropDef> actionOnInitialize = null) 
            : base(ns, name, domain, XsdLiteral.DOUBLE, node, actionOnInitialize)
        {
        }
    }
    public class RdfBooleanPropDef : RdfPropDef
    {
        public RdfBooleanPropDef(RdfNSDef ns, string name, IRdfURIClassDef domain,
            IUriNode node = null, Action<RdfPropDef> actionOnInitialize = null) 
            : base(ns, name, domain, XsdLiteral.BOOLEAN, node, actionOnInitialize)
        {
        }
    }


    public abstract class OwlPropertyDefBase: RdfPropDef
    {
        protected abstract void SetType();

        protected OwlPropertyDefBase(RdfNSDef ns, string name, 
            IRdfURIClassDef domain, IRdfClassDef range, 
            IUriNode node = null, Action<RdfPropDef> actionOnInitialize = null)
            : base(ns, name, null, node, actionOnInitialize)
        {
            SetProp(RDFSCommonDef.domain.QName, domain);
            SetProp(RDFSCommonDef.range.QName, range);
            SetType();
        }


        protected OwlPropertyDefBase(RdfNSDef ns, string name, 
            IEnumerable<IRdfURIClassDef> domains, IEnumerable<IRdfClassDef> ranges,
            IUriNode node = null, Action<RdfPropDef> actionOnInitialize = null)
            : base(ns, name, null, node, actionOnInitialize)
        {
            SetProps(RDFSCommonDef.domain.QName, domains);
            SetProps(RDFSCommonDef.range.QName, ranges);
            SetType();
        }



        public OwlPropertyDefBase SetCardinalityRestriction(IRdfURIClassDef domain, int? cardMin, int? cardMax)
        {

            if (domain != null)
            {
                List<(RdfPropDef, object)> restrictionKeyVals = new List<(RdfPropDef, object)>();

                if (cardMin != null && cardMax != null && cardMin == cardMax)
                {
                    restrictionKeyVals.Add((OWLCommonDef.cardinality, cardMin));
                }
                else
                {
                    if (cardMin != null)
                        restrictionKeyVals.Add((OWLCommonDef.minCardinality, cardMin));
                    if (cardMax != null)
                        restrictionKeyVals.Add((OWLCommonDef.maxCardinality, cardMax));
                }

                if (restrictionKeyVals.Any())
                    _setRestriction(domain, this, restrictionKeyVals);
            }
            return this;
        }

        public OwlPropertyDefBase SetAllValuesFromRestriction_OBJ(IRdfURIClassDef domain, IList<string> vals)
        {
            List<(RdfPropDef, object)> restrictionKeyVals = new List<(RdfPropDef, object)>();

            string enumTypeName = $"{domain.Name}.{this.Name}.Enum";

            OwlClassDef enumType = new OwlClassDef(domain.NS, enumTypeName, null, false, null);
            
            foreach(var val in vals)
            {
                RdfInstPersist inst = new RdfInstPersist(domain.NS, $"{enumTypeName}_{val}", enumType);
                inst.Label = val;
            }

            restrictionKeyVals.Add((OWLCommonDef.allValuesFrom, enumType));
            _setRestriction(domain, this, restrictionKeyVals);

            return this;
        }

        public void SetAllValuesFromRestriction_STR(IRdfURIClassDef domain, IList<string> vals)
        {
            List<(RdfPropDef, object)> restrictionKeyVals = new List<(RdfPropDef, object)>();

            string enumTypeName = $"{domain.Name}.{this.Name}.Enum";

            OwlClassDef enumType = new OwlClassDef(domain.NS, enumTypeName, null, false, null);

            List<object> _vals = new List<object>(vals);

            RdfReadOnlyList list = new RdfReadOnlyList(_vals, domain.NS, enumTypeName + "_0");

            enumType.AddProp(OWLCommonDef.oneOf.QName, list);

            restrictionKeyVals.Add((OWLCommonDef.allValuesFrom, enumType));
            _setRestriction(domain, this, restrictionKeyVals);
        }



        private void _setRestriction(IRdfURIClassDef domain,
            RdfPropDef property, List<(RdfPropDef, object)> restrictionKeyVals)
        {
            if (domain.Node != null && domain.NS != null && domain.Node.Graph != null)
            {
                IEnumerable<string> _existing = domain.Node.Graph.AllNodes
                    .Where(t => t is IUriNode u)
                    .Select(t => (t as IUriNode).Uri.AbsoluteUri)
                    .Where(s => s.StartsWith(domain.FullPath));
                HashSet<string> existing = new HashSet<string>(_existing);

                int i = 1;
                string suffix = ".restriction_" + i;

                while (existing.Contains(domain.FullPath + suffix))
                {
                    i++;
                    suffix = ".restriction_" + i;
                }

                OwlRestrictionDef restriction = new OwlRestrictionDef(domain.NS, domain.Name + suffix);
                restriction.AddProp(OWLCommonDef.onProperty.QName, property);

                foreach(var pair in restrictionKeyVals)
                {
                    restriction.AddProp(pair.Item1.QName, pair.Item2);
                }

                var subClassOf = domain.NS.Graph.CreateUriNode(RDFSCommonDef.subClassOf.QName);
                domain.NS.Graph.Assert(domain.Node, subClassOf, restriction.Node);
            }
            else
            {
                //pass
            }

        }
    }

    public class OwlObjectPropertyDef: OwlPropertyDefBase
    {
        public OwlObjectPropertyDef(RdfNSDef ns, string name, IRdfURIClassDef domain, IRdfClassDef range,
            IUriNode node = null, Action<RdfPropDef> actionOnInitialize = null)
            : base(ns, name, domain, range, node, actionOnInitialize)
        {

        }

        public OwlObjectPropertyDef(RdfNSDef ns, string name, IEnumerable<IRdfURIClassDef> domains, IEnumerable<IRdfClassDef> ranges, 
            IUriNode node = null, Action<RdfPropDef> actionOnInitialize = null) 
            : base(ns, name, domains, ranges, node, actionOnInitialize)
        {

        }

        protected override void SetType()
        {
            this.AddType(OWLCommonDef.ObjectProperty);
        }
    }

    public class OwlDatatypePropertyDef : OwlPropertyDefBase
    {

        public OwlDatatypePropertyDef(RdfNSDef ns, string name, IRdfURIClassDef domain, IRdfClassDef range, 
            IUriNode node = null, Action<RdfPropDef> actionOnInitialize = null) 
            : base(ns, name, domain, range, node, actionOnInitialize)
        {
            
        }

        public OwlDatatypePropertyDef(RdfNSDef ns, string name, IEnumerable<IRdfURIClassDef> domains, IEnumerable<IRdfClassDef> ranges, 
            IUriNode node = null, Action<RdfPropDef> actionOnInitialize = null) 
            : base(ns, name, domains, ranges, node, actionOnInitialize)
        {

        }

        protected override void SetType()
        {
            this.AddType(OWLCommonDef.DatatypeProperty);
        }

    }
}
