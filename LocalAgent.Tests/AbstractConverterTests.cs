using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LocalAgent.Serializers;
using Xunit;

namespace LocalAgent.Tests
{
    public class AbstractConverterTests
    {
        public class Model
        {
            public IExpectation[] Expectation { get; set; }
        }

        public class BoundsExpectation : Expectation
        {
            public string Bounds { get; set; }
        }

        public class TimeExpectation : Expectation
        {
            public string Time { get; set; }
        }

        public class CentroidExpectation : Expectation
        {
            public string Centroid { get; set; }

            public IList<IExpectation> Expectations { get; set; }
        }

        [Fact]
        public void TestOne()
        {
            Model model = new Model
            {
                Expectation = new Expectation[] 
                    {
                        new BoundsExpectation() { Bounds = "abc"}, 
                        new CentroidExpectation()
                        {
                            Centroid = "123", 
                            Expectations = new List<IExpectation>() {
                                new TimeExpectation()
                                {
                                    Time = "TUV"
                                }
                            }
                        }
                    }
            };

            var converter = new AbstractConverter();

            converter.AddResolver<ExpectationTypeResolver<IExpectation>>()
                .AddMapping<BoundsExpectation>(nameof(BoundsExpectation.Bounds))
                .AddMapping<CentroidExpectation>(nameof(CentroidExpectation.Centroid))
                .AddMapping<TimeExpectation>(nameof(TimeExpectation.Time));

            converter.AddResolver<AggregateExpectationTypeResolver<IExpectation>>();
    
            var yaml = converter.Serialize<Model>(model);

            var model2 = converter.Deserializer<Model>(yaml);
        }
    }
}
