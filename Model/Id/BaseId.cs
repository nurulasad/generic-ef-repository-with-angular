using System;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace GenericRepository.Model.Id
{
    [Serializable]
    public abstract class BaseId<TBaseType> : IIdentifier
    {
        private TBaseType value;
        public TBaseType Value
        {
            get
            {
                return value;
            }
                   
            set
            {
                this.value = value;
            }
        }

        public BaseId(TBaseType value)
        {
            this.value = value;
        }
        
        public abstract bool IsValid();
        
        public override string ToString()
        {
            throw new Exception("This method should be implemented in the inheriting class.");
        }
    }
    

    [Serializable]
    public abstract class LongId : BaseId<long>, IComparable
    {
        // Must have a parameterless constructor for serialization
        public LongId() : base(0) { }
        public LongId(long value) : base(value) { }

        public override string ToString()
        {
            return Value.ToString();
        }

		public int CompareTo(object obj)
		{
			if (this.GetType() != obj.GetType())
			{
				throw new Exception("Cannot compare objects of different types");
			}

			long other = ((LongId) obj).Value;

			if (this.Value < other)
			{
				return -1;
			}
			else if (this.Value > other)
			{
				return 1;
			}
			else
			{
				return 0;
			}
		}

		public static bool IsValid(LongId id)
		{
			if ((id != null)&&(id.IsValid()))
			{
				return true;
			}
			return false;
		}

	    public override bool IsValid()
        {
            return Value > 0;
        }

   
    }
    
}
