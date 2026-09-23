using System;
using System.Collections;
using System.Collections.Generic;
using DonChan.TelemetryProbe;
class V { public double x, y, z; public V(double a, double b, double c) { x=a; y=b; z=c; } }
class E { public int entityId=1; public V position=new V(0,0,0); public bool dead; public object attackTarget; public bool IsDead() { return dead; } }
class Unknown { public V position=new V(0,0,0); }
class W { public IEnumerable EntityAlives; }
class Broken : IEnumerable { public IEnumerator GetEnumerator() { yield return new E(); throw new Exception(); } }
class Test {
 static int checks;
 static void Check(bool ok, string name) { checks++; if(!ok) throw new Exception(name); }
 static Dictionary<string,object> Run(E p, params object[] entities) { return NearbyObservation.Collect(new W { EntityAlives=entities },p); }
 static List<object> Rows(Dictionary<string,object> d) { return (List<object>)d["records"]; }
 static void Main() {
  var p=new E { entityId=7 };
  var d=Run(p,new E { position=new V(15,100,0), attackTarget=p },new E { position=new V(15.01,0,0) },new E { dead=true });
  Check(Rows(d).Count==1,"horizontal inclusive range and dead exclusion");
  Check((string)((Dictionary<string,object>)Rows(d)[0])["targetState"]=="player_reference","target identity");
  Check((bool)d["recordsComplete"],"complete scan");
  d=Run(p,new Unknown()); Check(!(bool)d["recordsComplete"],"unknown death not alive");
  Check((string)((Dictionary<string,object>)Rows(d)[0])["lifeState"]=="unknown","unknown preserved");
  d=Run(p,new E { position=new V(double.NaN,0,0) }); Check(!(bool)d["recordsComplete"],"nonfinite position");
  d=NearbyObservation.Collect(new W { EntityAlives=new Broken() },p);
  Check(!(bool)d["scanComplete"] && Rows(d).Count==1,"partial enumeration");
  d=NearbyObservation.Collect(new W(),p); Check(!(bool)d["scanComplete"],"missing list");
  d=Run(p,new E()); Check((string)((Dictionary<string,object>)Rows(d)[0])["targetState"]=="none_observed","read null distinguished");
  d=Run(p,new Unknown()); Check((string)((Dictionary<string,object>)Rows(d)[0])["targetState"]=="unknown","absent target member");
  var many=new object[129]; for(int i=0;i<many.Length;i++) many[i]=new E();
  d=Run(p,many); Check((bool)d["scanComplete"] && !(bool)d["recordsComplete"] && (int)d["omittedRecords"]==1,"bounded output explicit omission");
  d=Run(p); Check((bool)d["recordsComplete"] && Rows(d).Count==0,"valid empty differs from failure");
  Check((string)d["classificationStatus"]=="pending","no enemy classification claim");
  Console.WriteLine("Nearby observation: " + checks + " assertions passed");
 }
}
