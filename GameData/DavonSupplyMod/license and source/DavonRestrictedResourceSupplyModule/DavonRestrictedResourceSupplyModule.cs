//Written by Flip van Toly for KSP community
//License GPL (GNU General Public License)
// Namespace Declaration 
 using System;
 using System.Collections.Generic;
 using System.Linq;
 using System.Text;
 using UnityEngine;

using DavonSupplyMod_KACWrapper;

namespace DavonSupplyMod
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
	public class DavonRestrictedResourceSupplyModule : PartModule
	{
		//display status
		[KSPField(isPersistant = false, guiActive = true, guiName = "SYS~")]
		public string status;

        //display the time module has been static
		[KSPField(isPersistant = false, guiActive = true, guiName = "Orbit Static Time", guiUnits = " days")]
		public double staticTime;

		[KSPField(isPersistant = true, guiActive = false, guiName = "KAC alarm ID")]
		public string KACalarmID;

		//values to save orbit
		[KSPField(isPersistant = true, guiActive = false)]
		public float SMAsave;
		[KSPField(isPersistant = true, guiActive = false)]
		public float ECCsave;
		[KSPField(isPersistant = true, guiActive = false)]
		public float INCsave;
		[KSPField(isPersistant = true, guiActive = false)]
		public float LPEsave;
		[KSPField(isPersistant = true, guiActive = false)]
		public float LANsave;
		[KSPField(isPersistant = true, guiActive = false)]
		public float MNAsave;
		[KSPField(isPersistant = true, guiActive = false)]
		public string REFsave;
		[KSPField(isPersistant = true, guiActive = false)]
		public float EPHsave;		

		//value to supplies delivered
		[KSPField(isPersistant = true, guiActive = true, guiName = "Supplies", guiUnits = "%")]
		public float supplies;
		
		//value to save last delivery request
		[KSPField(isPersistant = true, guiActive = false)]
		public string request;
        //value to save content of the request
        [KSPField(isPersistant = true, guiActive = false)]
        public string requestContent;


		//values for saving activation restrictions
		[KSPField(isPersistant = true, guiActive = true, guiName = "Body")]
		public string Body;
		[KSPField(isPersistant = false, guiActive = true, guiName = "Orbit limit", guiUnits = "km")]
		public float OrbitLimit;
		[KSPField(isPersistant = true, guiActive = false, guiName = "Max Semi-mayor Axis", guiUnits = "km")]
		public float MaxSemiMayorAxis;
		
		//the value which determines how much an orbit parameter may deviate before it's considered non static
		[KSPField(isPersistant = false, guiActive = false)]
		public float MaxDeviationValue;	
		//string with the resources for which the partmodule must function
		[KSPField(isPersistant = false, guiActive = false)]
		public string ManagedResources;
		//string with with the factor how many times the parts resource amount should be delivered per delivery
		[KSPField(isPersistant = false, guiActive = false)]		
		public float DeliveryAmountFactor;
		//the factor for the supply-activation amount based on maximum capacity of recourse
		[KSPField(isPersistant = false, guiActive = false)]
		public float SupplyActivationFactor;
        [KSPField(isPersistant = false, guiActive = false)]
        public string CostPerTon = "";
		//DeliveryDuration
		[KSPField(isPersistant = false, guiActive = false)]
		public string BaseDeliveryTime;
		[KSPField(isPersistant = false, guiActive = false)]
		public string DeliveryTimeList;


        double nextchecktime = 0;

        public override void OnUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            if (vessel.packed || !vessel.loaded)
            {
                nextchecktime = 0;
                return;
            }
            if (nextchecktime == 0)
            {
                nextchecktime = Planetarium.GetUniversalTime() + 3;
                return;
            }
            if (Planetarium.GetUniversalTime() > nextchecktime)
            {
                nextchecktime = Planetarium.GetUniversalTime() + 60;
                if (String.IsNullOrEmpty(request) || "0" == request.Substring(0,1))//check if there is delivery pending
			    {
                    //nothing
			    }
                else
                {
                    checkdelivery();
                }
            }
            
        }

		internal void Start()
		{
			KACWrapper.InitKACWrapper();
		}

	    [KSPEvent(name = "supply", isDefault = false, guiActive = true, guiName = "Supply")]
		public void supply()
		{
            if (vessel.situation != Vessel.Situations.ORBITING)
            {
                status = "not in orbit";
                return;
            }
            
            if (!checkPLanet()) 
			{
				return;
			}
			if (isActivated())
			{//If so extra delivery is not nessesary.
				status = "already supply-activated";
				UIcontrol();
				return;
			}



			if (staticorbit() == 0) 
			{//orbit is not stable since last saved orbit. 
				if (supplies > 0) //already in the proces of activation
				{	//reset
					status = "no static orbit - resetting";
					supplies = 0;
					return;
				}
				else //not yet in the process of activation
				{
					status = "deploying in orbit";
					saveorbit();
					Body = vessel.orbit.referenceBody.name;
					MaxSemiMayorAxis = (float)Math.Round((vessel.orbit.semiMajorAxis*1.2/1000),0);

                    if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
                    {
                        supplies = 100;
                        UIcontrol();
                    }
					return;
				}
			}

			//process supply

			string[] SplitArray = ManagedResources.Split(',');
			string[] arrResource = new string[SplitArray.Length];
			for (int runs = 0; runs < SplitArray.Length; runs++)
			{
				arrResource[runs] = SplitArray[runs].Replace(" ", string.Empty);
			}
			
			//determine lowest ratio fuel to max capacity and reduce all fuel by that ratio
			double lowestratio = 1;
			foreach (PartResource r in part.Resources)
			{
				if (arrResource.Contains(r.info.name))
				{
					if (lowestratio > r.amount/r.maxAmount) {lowestratio = r.amount/r.maxAmount; }
				}
			}
			foreach (PartResource r in part.Resources)
			{
				if (arrResource.Contains(r.info.name))
				{	
				r.amount = r.amount - (lowestratio*r.maxAmount);
				if (r.amount < 0) r.amount = 0; 
				}
			}
			
			//add supplies to ratio
			supplies = supplies + (float)(lowestratio/SupplyActivationFactor*100);
			if (supplies > 99.9) 
			{
				supplies = 100;
			}
			
			status = "accepting supply";

			//Update UI
			UIcontrol();
		}
		
		[KSPEvent(name = "requestdelivery", isDefault = false, guiActive = true, guiName = "Request delivery")]
		public void requestdelivery()
		{
			if (!checkPLanet())
			{
				return;
			}
			if (!isActivated())
			{//If not no resources can be supplied by module.
				status = "not yet supply-activated";
				return;
			}

			if (vessel.orbit.semiMajorAxis/1000 > MaxSemiMayorAxis)//check if module is not on an orbit which has a higher semi mayor axis than the Maximum Semi mayor axis altitude 
			{//Inform and don't supply recources.  
				status = "orbit limit exceeded";
				//Update UI
				UIcontrol();
				return; 
			}

			if (!String.IsNullOrEmpty(request) && "1" == request.Substring(0,1))//check if there is delivery pending
			{
				status = "delivery already underway";
				UIcontrol();
				return;
			}
			
			if (staticorbit() == 0) 
			{//orbit is not stable since last saved orbit. 
				//Set saved deployed orbit to new parameters
				saveorbit();
			}
			else
			{//orbit is stable since last saved orbit. 
				//Even though orbit is stable set saved deployed orbit to new parameters, so orbit can drift from current parameters to give player some extra leeway. But keep oldest epoch since that's the time from which orbit has been stable.
				syncorbit();
			}

            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                double bill = billingamount();
                if (Funding.Instance.Funds > bill)
                {
                    Funding.Instance.AddFunds(-bill, TransactionReasons.Any);
                }
                else
                {
                    status = "insufficient funds";
                    return;
                }
            }

            //request delivery
			request = "1";
			request = request + ": ";
			request = request + Planetarium.GetUniversalTime();
            fillrequestcontent();
			checkdelivery ();
			nextchecktime = Planetarium.GetUniversalTime() + 60;
			//Update UI
			UIcontrol();
		}
		
		[KSPEvent(name = "canceldelivery", isDefault = false, guiActive = true, guiName = "Cancel delivery")]
		public void canceldelivery()
		{
			if (!checkPLanet())
			{
				return;
			}
			if (!isActivated())
			{//If not no resources can be supplied by module.
				status = "not yet supply-activated";
				return;
			}
			
			if (String.IsNullOrEmpty(request) || "0" == request.Substring(0,1))//check if there is delivery pending
			{
				status = "no pending delivery";
				UIcontrol();
				return;
			}
			
			request = "0";
			status = "delivery cancelled";

			if (KACWrapper.APIReady) {
				if (KACalarmID != "") {
					try {
						KACWrapper.KAC.DeleteAlarm (KACalarmID);
					}
					catch {
						// Don't crash if there was some problem deleting the alarm
					}
				}
			}
			KACalarmID = "";
			
			//Update UI
			UIcontrol();
		}
		
		[KSPEvent(name = "checkdelivery", isDefault = false, guiActive = true, guiName = "Check delivery")]
		public void checkdelivery()
		{

            if (vessel.packed || !vessel.loaded)
            {
                return;
            }
            
            if (!checkPLanet())
			{
				return;
			}
			if (!isActivated())
			{//If not no resources can be supplied by module.
				status = "not yet supply-activated";
				return;
			}
			
			if (vessel.orbit.semiMajorAxis/1000 > MaxSemiMayorAxis)//check if module is not on an orbit which has a higher semi mayor axis than the Maximum Semi mayor axis altitude 
			{//Inform and don't supply recources.  
				status = "orbit limit exceeded";
				
				//Update UI
				UIcontrol();
				return; 
			}

			if (String.IsNullOrEmpty(request) || "0" == request.Substring(0,1))//check if there is a delivery pending
			{
				status = "no delivery pending";
				return;
			}

			//unpack request string
			string[] SplitArray = request.Split(':');
			string[] arrRequest = new string[SplitArray.Length];
			for (int runs = 0; runs < SplitArray.Length; runs++)
			{
				arrRequest[runs] = SplitArray[runs].Replace(" ", string.Empty);
			}
			
			double requesttime = Convert.ToDouble(arrRequest[1]);
			
			if (staticorbit() > (Planetarium.GetUniversalTime() - requesttime - 5))
			{//orbit is stable since request was made.

				
				string[] SplataArray = DeliveryTimeList.Split(',');
				string[] arrDeliveryTime = new string[SplataArray.Length];
				for (int runs = 0; runs < SplataArray.Length; runs++)
				{
				arrDeliveryTime[runs] = SplataArray[runs].Replace(" ", string.Empty);
				}
				
				//Use planet specific delivery time if available
				double requestduration = Convert.ToDouble(BaseDeliveryTime);
				for (int runs = 0; runs < SplataArray.Length; runs++)
				{
					if (arrDeliveryTime[runs] ==  vessel.orbit.referenceBody.name)
					{
						requestduration = Convert.ToDouble(arrDeliveryTime[runs+1]);
					}
				}
				
				if (requestduration > ((Planetarium.GetUniversalTime() - requesttime)/21600d))//check if delivery should be ready
				{
					status = "delivery in " + Math.Round((requestduration - (Planetarium.GetUniversalTime() - requesttime)/21600d),1) + " days";

					if (KACWrapper.APIReady) {
						string shipName = FlightGlobals.ActiveVessel.vesselName;
                    
						KACWrapper.KACAPI.KACAlarm a = null;
						if (KACalarmID != "") {
							a = KACWrapper.KAC.Alarms.FirstOrDefault (z => z.ID == KACalarmID);
						} else {
							KACalarmID = KACWrapper.KAC.CreateAlarm (
								KACWrapper.KACAPI.AlarmTypeEnum.Raw,
								"Davon delivery to "+shipName,
								requesttime + requestduration*21600d
							);
							a = KACWrapper.KAC.Alarms.FirstOrDefault (z => z.ID == KACalarmID);
							if (a != null) {
								a.AlarmAction = KACWrapper.KACAPI.AlarmActionEnum.KillWarp;
								a.AlarmMargin = 0;
								a.VesselID = FlightGlobals.ActiveVessel.id.ToString ();
								a.Notes = "Delivery of resources to " + shipName + " by Davon Tech Ltd";
							}
						}
					}
				}
				else
				{
					//deliver
					
					string[] ResourceArray = requestContent.Split(',');
					string[] arrResource = new string[ResourceArray.Length];
					for (int runs = 0; runs < ResourceArray.Length; runs++)
					{
						arrResource[runs] = ResourceArray[runs].Replace(" ", string.Empty);
					}

                    foreach (String st in ResourceArray)
                    {
                        string[] ResourceInfo = st.Split(':');

                        foreach (PartResource r in part.Resources)
                        {
                            if (r.info.name == ResourceInfo[0].Trim())
                            {
                                //how much needs to be delivered
                                double deliverAmount = Convert.ToDouble(ResourceInfo[1].Trim());

                                if (deliverAmount >= r.maxAmount - r.amount)
                                {
                                    //first deliver to part
                                    deliverAmount = deliverAmount - (r.maxAmount - r.amount);
                                    r.amount = r.maxAmount;

                                    //deliver to other parts
                                    foreach (Part op in vessel.parts)
                                    {

                                        foreach (PartResource or in op.Resources)
                                        {
                                            if (or.info.name == r.info.name)
                                            {
                                                if (deliverAmount >= or.maxAmount - or.amount)
                                                {
                                                    deliverAmount = deliverAmount - (or.maxAmount - or.amount);
                                                    or.amount = or.maxAmount;
                                                }
                                                else
                                                {
                                                    or.amount = or.amount + deliverAmount;
                                                    deliverAmount = 0;
                                                }
                                            }
                                        }
                                    }
                                    //part.RequestResource(r.info.name, (float)-r.maxAmount*DeliveryAmountFactor);
                                }
                                else
                                {
                                    r.amount = r.amount + deliverAmount;
                                    deliverAmount = 0;
                                }
                            }
                        }
                    }
					request = "0";
                    requestContent = "";
					status = "delivery completed";
                    KACalarmID = "";
				}
			}
			else
			{//orbit is not stable since request.
				//Redeploy from the new orbit, cancel deliveries.
				status = "no static orbit - delivery cancelled";
				request = "0";
                requestContent = "";
				saveorbit();
			}
			
			//Update UI
			UIcontrol();
		}
		


		private void UIcontrol()
		{
			if (supplies < 100)
			{
				Events["supply"].guiActive = true;
				Events["requestdelivery"].guiActive = false;
				Events["canceldelivery"].guiActive = false;
				Events["checkdelivery"].guiActive = false;
			}
			else
			{
				Events["supply"].guiActive = false;
				Events["requestdelivery"].guiActive = true;
				Events["canceldelivery"].guiActive = true;
				Events["checkdelivery"].guiActive = true;	
			}
			
			if (!String.IsNullOrEmpty(request) && "1" == request.Substring(0,1))//check if there is delivery pending
			{
				Events["requestdelivery"].guiActive = false;
				Events["canceldelivery"].guiActive = true;
			}
			else
			{
				Events["requestdelivery"].guiActive = true;
				Events["canceldelivery"].guiActive = false;
			}
			
			OrbitLimit  = (float)Math.Round((vessel.orbit.semiMajorAxis/1000)-MaxSemiMayorAxis,0);
			
			if (supplies > 0 && supplies < 100)
			{
				extend();	
			}
			else if (supplies >= 100)
			{
				if (!String.IsNullOrEmpty(request) && "1" == request.Substring(0,1))
				{
					extend();
				}
				else
				{
					retract();	
				}
			}
			else
			{
				retract();
			}
		}

        public double billingamount()
        {
            double resMass = 0;
            double resCost = 0;
            double costperton = 0;

            string[] SplutArray = ManagedResources.Split(',');
            string[] arrResource = new string[SplutArray.Length];
            for (int runs = 0; runs < SplutArray.Length; runs++)
            {
                arrResource[runs] = SplutArray[runs].Replace(" ", string.Empty);
            }

            foreach (PartResource r in part.Resources)
            {
                if (arrResource.Contains(r.info.name))
                {
                    resMass = resMass + mass(r.info.name, (r.maxAmount - r.amount));
                    resCost = resCost + cost(r.info.name, (r.maxAmount - r.amount));
                }
            }

            string[] SplataArray = CostPerTon.Split(',');
            string[] arrCostPerTon = new string[SplataArray.Length];
            for (int runs = 0; runs < SplataArray.Length; runs++)
            {
                arrCostPerTon[runs] = SplataArray[runs].Replace(" ", string.Empty);
            }

            //Use planet specific delivery time if available
            double requestduration = Convert.ToDouble(BaseDeliveryTime);
            for (int runs = 0; runs < SplataArray.Length; runs++)
            {
                if (arrCostPerTon[runs] == vessel.orbit.referenceBody.name)
                {
                    costperton = Convert.ToDouble(arrCostPerTon[runs + 1]);
                }
            }
            //print("Cpt " + costperton);
            return ((costperton * resMass) + resCost);

        }

        public void fillrequestcontent()
        {

            requestContent = "";

            string[] SplutArray = ManagedResources.Split(',');
            string[] arrResource = new string[SplutArray.Length];
            for (int runs = 0; runs < SplutArray.Length; runs++)
            {
                arrResource[runs] = SplutArray[runs].Replace(" ", string.Empty);
            }

            foreach (PartResource r in part.Resources)
            {
                if (arrResource.Contains(r.info.name))
                {
                    if (requestContent == "")
                    {
                        requestContent = r.info.name + ":" + (r.maxAmount - r.amount).ToString();
                    }
                    else
                    {
                        requestContent = requestContent + "," + r.info.name + ":" + (r.maxAmount - r.amount).ToString();
                    }
                }
            }
        }


        public double mass(string resourceName, double amount)
        {
            PartResourceDefinition prd = PartResourceLibrary.Instance.GetDefinition(resourceName);
            return (amount * prd.density);
        }

        public double cost(string resourceName, double amount)
        {
            PartResourceDefinition prd = PartResourceLibrary.Instance.GetDefinition(resourceName);
            return (amount * prd.unitCost);
        }

		private double staticorbit()
		{
			//Determine deviation between saved values and current values if they don't changed to much update the staticTime and return staticTime if not return zero.
			//the parameter AnomalyAtEpoch changes over time and must be excluded from analysis
			
			bool[] parameters = new bool[5];
			parameters[0] = false;
			parameters[1] = false;
			parameters[2] = false;

			//lenght
				if (MaxDeviationValue/100 > Math.Abs(((vessel.orbit.semiMajorAxis - SMAsave) / SMAsave))) {parameters[0] = true;} 
			//ratio
				if (MaxDeviationValue/100 > Math.Abs(vessel.orbit.eccentricity - ECCsave)) {parameters[1] = true;} 
			
			float angleD = MaxDeviationValue;
			//angle
				if (angleD > Math.Abs(vessel.orbit.inclination - INCsave) 		|| angleD > Math.Abs(Math.Abs(vessel.orbit.inclination - INCsave) - 360))		{parameters[2] = true;}
				
			//print("SMA " + parameters[0] + ((vessel.orbit.semiMajorAxis - SMAsave) / SMAsave)	);
			//print("ECC " + parameters[1] + Math.Abs(vessel.orbit.eccentricity - ECCsave) 		);
			//print("INC " + parameters[2] + Math.Abs(vessel.orbit.inclination - INCsave) + " or " + Math.Abs(Math.Abs(vessel.orbit.inclination - INCsave) - 360)	);
	
			if(parameters[0] == false || parameters[1] == false || parameters[2] == false)
			{
				staticTime = 0;
				return 0;
			}
			else
			{
                if (Planetarium.GetUniversalTime() - EPHsave > 0)
				{
                    staticTime = Math.Round((Planetarium.GetUniversalTime() - EPHsave) / 21600, 2);
                    return (Planetarium.GetUniversalTime() - EPHsave);
				}
				else
				{
					staticTime = 0;
					return 0;
				}
			}
		}

		
		private bool isActivated()
		{//check if module has already been supply-activated.
			return (supplies >= 100);	
		}
		
		private bool checkPLanet()
		{//check if the module is still orbiting the same planet as where the module was supply-activated
			if (vessel.orbit.referenceBody.name != Body && !String.IsNullOrEmpty(Body) || !allowedPLanet())
			{
				factoryreset();
				status = "altered SOI - factory reset";
				return false;
			}
			else
			{
				return true;
			}
		}

        private bool allowedPLanet()
        {//is this planet allowed
            string[] SplitaArray = DeliveryTimeList.Split(',');
            string[] arrDeliveryTime = new string[SplitaArray.Length];

            for (int runs = 0; runs < SplitaArray.Length; runs++)
            {
                if (SplitaArray[runs].Trim() == vessel.orbit.referenceBody.name)
                {
                    return(true);
                }
            }
            return (false);
        }

		private void saveorbit()
		{					
			SMAsave = (float)vessel.orbit.semiMajorAxis;
			ECCsave = (float)vessel.orbit.eccentricity;
			INCsave = (float)vessel.orbit.inclination;
			LPEsave = (float)vessel.orbit.argumentOfPeriapsis;
			LANsave = (float)vessel.orbit.LAN;
			MNAsave = (float)vessel.orbit.meanAnomalyAtEpoch;
			REFsave = vessel.orbit.referenceBody.name;
			EPHsave = (float)Planetarium.GetUniversalTime();
		}


		
		private void syncorbit()
		{					
			SMAsave = (float)vessel.orbit.semiMajorAxis;
			ECCsave = (float)vessel.orbit.eccentricity;
			INCsave = (float)vessel.orbit.inclination;
			LPEsave = (float)vessel.orbit.argumentOfPeriapsis;
			LANsave = (float)vessel.orbit.LAN;
			MNAsave = (float)vessel.orbit.meanAnomalyAtEpoch;
			REFsave = vessel.orbit.referenceBody.name;
		}
		
		private void factoryreset()
		{
			supplies = 0;
			request = "0";
			
			SMAsave = 0;
			ECCsave = 0;
			INCsave = 0;
			LPEsave = 0;
			LANsave = 0;
			MNAsave = 0;
			REFsave = null;
			EPHsave = 0;

			Body = null;
			MaxSemiMayorAxis = 0;
			
			//remove resources
			foreach (PartResource r in part.Resources)
			{
				r.amount = 0;
			}
			UIcontrol();
		}
        
        
		void extend()
		{
			ModuleAnimateGeneric aModuleAnimateGeneric = new ModuleAnimateGeneric();
			aModuleAnimateGeneric = part.Modules.OfType<ModuleAnimateGeneric>().FirstOrDefault();
            if (aModuleAnimateGeneric == null) { return; }
            if (aModuleAnimateGeneric.Events["Toggle"].guiName == "Extend" && aModuleAnimateGeneric.Events["Toggle"].guiActive == true)
            {
                aModuleAnimateGeneric.Toggle();
            }
		}
		
		void retract()
		{
			ModuleAnimateGeneric aModuleAnimateGeneric = new ModuleAnimateGeneric();
			aModuleAnimateGeneric = part.Modules.OfType<ModuleAnimateGeneric>().FirstOrDefault();	
			if (aModuleAnimateGeneric == null) { return; }
			if(aModuleAnimateGeneric.Events["Toggle"].guiName == "Retract" && aModuleAnimateGeneric.Events["Toggle"].guiActive == true)				
			{
				aModuleAnimateGeneric.Toggle();
			}			
		}
    }
}




