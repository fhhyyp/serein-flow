using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Serein.Library.Utils.EmitHelper;

namespace Serein.Library.Entity
{
    /// <summary>
    /// 委托描述
    /// </summary>
    public class DelegateDetails
    {
        public DelegateDetails(EmitMethodType EmitMethodType, Delegate EmitDelegate)
        {
            this._emitMethodType = EmitMethodType;
            this._emitDelegate = EmitDelegate;
        }
        public void Upload(EmitMethodType EmitMethodType, Delegate EmitDelegate)
        {
            _emitMethodType = EmitMethodType;
            _emitDelegate = EmitDelegate;
        }
        private Delegate _emitDelegate;
        private EmitMethodType _emitMethodType;
        public Delegate EmitDelegate { get => _emitDelegate; }
        public EmitMethodType EmitMethodType { get => _emitMethodType; }
    }
}
