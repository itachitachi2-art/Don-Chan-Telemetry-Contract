using System;
using System.Collections;
using System.Collections.Generic;
using DonChan.TelemetryProbe;
class V { public double x, y, z; public V(double a, double b, double c) { x=a; y=b; z=c; } }
class E { public int entityClass=0; public int entityId=1; public V position=new V(0,0,0); public bool dead; public object attackTarget; public bool IsDead() { return dead; } }
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
  Check((string)d["classificationStatus"]=="complete","no enemy classification claim");
  Check(DonChan.Shared.NearbyTargetClassifier.Classify(typeof(E),"animalZombieBear")=="zombie_bear","bear included");
  Check(DonChan.Shared.NearbyTargetClassifier.Classify(typeof(E),"animalDireWolf")=="dire_wolf","dire wolf included");
  Check(DonChan.Shared.NearbyTargetClassifier.Classify(typeof(E),"animalBossGrace")=="grace","grace included");
  Check(DonChan.Shared.NearbyTargetClassifier.Classify(typeof(E),"animalZombieDog")=="zombie_dog","dog included");
  Check(DonChan.Shared.NearbyTargetClassifier.Classify(typeof(E),"animalZombieVultureRadiated")=="vulture","radiated vulture included");
  Check(DonChan.Shared.NearbyTargetClassifier.Classify(typeof(EntityZombie),null)=="humanoid_zombie","human exact type");
  Check(DonChan.Shared.NearbyTargetClassifier.Classify(typeof(E),"animalWolf")=="excluded","normal wolf excluded");
  Check(DonChan.Shared.NearbyTargetClassifier.Classify(typeof(E),"modZombieBear")=="unknown","no substring guess");
  Check(DonChan.Shared.NearbyTargetClassifier.ResolveClassName(new E())==null,"missing registry remains unknown");
  EntityClass.list[9]=new EntityClass { entityClassName="animalBossGrace" };
  Check(DonChan.Shared.NearbyTargetClassifier.ResolveClassName(new E { entityClass=9 })=="animalBossGrace","registry resolution");
  d=Run(p,new E { entityClass=9 });
  Check((int)d["classifiedAliveTargets"]==1 && (string)d["classificationStatus"]=="complete","recognized target count");
  d=Run(p,new E { entityClass=999 });
  Check((int)d["classifiedAliveTargets"]==0 && (string)d["classificationStatus"]=="partial","unknown not absence");
  Check(DonChan.Shared.NearbyTargetClassifier.Classify(typeof(EntityDrone),null)=="excluded","drone excluded");
  Check(DonChan.Shared.NearbyTargetClassifier.Classify(typeof(EntityMotorcycle),null)=="excluded","vehicle ancestry excluded");
  Console.WriteLine("Nearby observation: " + checks + " assertions passed");
 }
}

class EntityZombie {}

class Registry { internal Dictionary<int,EntityClass> Dict=new Dictionary<int,EntityClass>(); public EntityClass this[int id] { set { Dict[id]=value; } } }
class EntityClass { public static Registry list=new Registry(); public string entityClassName;
 public static string GetEntityClassName(int id) { EntityClass v; return list.Dict.TryGetValue(id,out v) ? v.entityClassName : "null"; } }
class EntityDrone {}
class EntityVehicle {}
class EntityMotorcycle : EntityVehicle {}


