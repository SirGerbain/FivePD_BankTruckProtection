using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Xml.Linq;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePD.API;
using FivePD.API.Utils;

namespace SirGerbain_BankTruckRobberyAlternative
{
    [CalloutProperties("Bank Truck Protection", "SirGerbain", "1.1")]
    public class SirGerbain_BankTruckRobberyAlternative : Callout
    {
        private Ped guard1;
        private Ped guard2;
        private Vehicle bankTruck;
        private List<Ped> robbers = new List<Ped>();
        private Vehicle robberVehicle;
        private Vector3 spawnLocation;
        private List<PedHash> guardHashList = new List<PedHash>();
        private List<PedHash> robberHashList = new List<PedHash>();
        private List<VehicleHash> vehicleHashList = new List<VehicleHash>();
        private float tickTimer = 0f;
        private float tickInterval = 1f;

        private bool awaitContact = true;
        private bool madeContact = false;
        private bool awaitAttack = true;
        private bool robbersCantTakeTheHeat = false;

        public SirGerbain_BankTruckRobberyAlternative()
        {
            Random rnd = new Random();
            float offsetX = rnd.Next(100, 700);
            float offsetY = rnd.Next(100, 700);
            spawnLocation = World.GetNextPositionOnStreet(Game.PlayerPed.GetOffsetPosition(new Vector3(offsetX, offsetY, 0)));

            vehicleHashList.Add(VehicleHash.Baller4);

            robberHashList.Add(PedHash.Blackops01SMY);
            robberHashList.Add(PedHash.Blackops02SMY);
            robberHashList.Add(PedHash.Blackops03SMY);
            robberHashList.Add(PedHash.Robber01SMY);

            guardHashList.Add(PedHash.Prisguard01SMM);
            guardHashList.Add(PedHash.Sheriff01SMY);
            guardHashList.Add(PedHash.Sheriff01SFY);

            InitInfo(spawnLocation);

            ShortName = "Bank Truck Protection";
            CalloutDescription = "A bank truck is being robbed on the street.";
            ResponseCode = 3;
            StartDistance = 200f;
        }
        public async override Task OnAccept()
        {
            InitBlip();
            UpdateData();

            PlayerData playerData = Utilities.GetPlayerData();
            string displayName = playerData.DisplayName;
            Notify("~r~[PDM 911] ~y~Officer ~b~" + displayName + ",~y~ Gruppe 6 needs back up!");
            DrawSubtitle("Gruppe 6 reported some suspicious activity. Go keep an eye out, they are closeby", 7000);

        }

        public async override void OnStart(Ped player)
        {
            base.OnStart(player);
            await setupCallout();
            Tick += OnTick;
        }

        public async Task setupCallout()
        {
            Random random = new Random();

            guard1 = await SpawnPed(guardHashList[random.Next(guardHashList.Count)], spawnLocation + new Vector3(0, 2, 0));
            guard1.AlwaysKeepTask = true;
            guard1.BlockPermanentEvents = true;
            guard1.Weapons.Give(WeaponHash.Pistol, 250, true, true);

            guard2 = await SpawnPed(guardHashList[random.Next(guardHashList.Count)], spawnLocation + new Vector3(0, 3, 0));
            guard2.AlwaysKeepTask = true;
            guard2.BlockPermanentEvents = true;
            guard2.Weapons.Give(WeaponHash.Pistol, 250, true, true);

            Vector3 coords = guard1.Position;
            Vector3 closestVehicleNodeCoords;
            float roadheading;
            OutputArgument tempcoords = new OutputArgument();
            OutputArgument temproadheading = new OutputArgument();
            Function.Call<Vector3>(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, coords.X, coords.Y, coords.Z, tempcoords, temproadheading, 1, 3, 0);
            closestVehicleNodeCoords = tempcoords.GetResult<Vector3>();
            roadheading = temproadheading.GetResult<float>();

            bankTruck = await SpawnVehicle(VehicleHash.Stockade, spawnLocation);
            bankTruck.Heading= roadheading;
            bankTruck.AttachBlip().IsFriendly= true;
            guard1.SetIntoVehicle(bankTruck, VehicleSeat.Driver);
            guard2.SetIntoVehicle(bankTruck, VehicleSeat.Passenger);
            guard1.Task.CruiseWithVehicle(bankTruck, 20f, 443);

        }

        public async Task setupRobbers()
        {
            Random rnd = new Random();
            float offsetX = rnd.Next(100, 175);
            float offsetY = rnd.Next(100, 175);
            Vector3 robberLocation = World.GetNextPositionOnStreet(bankTruck.Position + new Vector3(offsetX, offsetY, 0));

            for (int i = 0; i < 4; i++)
            {
                Ped robber = await SpawnPed(robberHashList[rnd.Next(0, robberHashList.Count)], robberLocation);
                    robber.AlwaysKeepTask = true;
                    robber.BlockPermanentEvents = true;
                    if (i > 0) //exclude driver for now
                    {
                        robber.Weapons.Give(WeaponHash.MicroSMG, 500, true, true);
                    }
                robbers.Add(robber);
            }

            Vector3 coords = robbers[0].Position;
            Vector3 closestVehicleNodeCoords;
            float roadheading;
            OutputArgument tempcoords = new OutputArgument();
            OutputArgument temproadheading = new OutputArgument();
            Function.Call<Vector3>(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, coords.X, coords.Y, coords.Z, tempcoords, temproadheading, 1, 3, 0);
            closestVehicleNodeCoords = tempcoords.GetResult<Vector3>();
            roadheading = temproadheading.GetResult<float>();

            Vector3 playerPos = Game.PlayerPed.Position;
            float playerHeading = Game.PlayerPed.Heading;
            Vector3 spawnPos = playerPos - (Vector3.Normalize(new Vector3((float)Math.Sin(playerHeading), (float)Math.Cos(playerHeading), 0f)) * 100f);
            robberLocation = World.GetNextPositionOnStreet(spawnPos);

            robberVehicle = await SpawnVehicle(vehicleHashList[rnd.Next(vehicleHashList.Count)], robberLocation);
            robberVehicle.Mods.PrimaryColor = VehicleColor.MetallicBlack;
            robberVehicle.Mods.SecondaryColor = VehicleColor.MetallicBlack;
            robberVehicle.Mods.PearlescentColor = VehicleColor.MetallicBlack;
            robberVehicle.Heading= roadheading;

            robbers[0].SetIntoVehicle(robberVehicle, VehicleSeat.Driver);
            robbers[1].SetIntoVehicle(robberVehicle, VehicleSeat.Passenger);
            robbers[2].SetIntoVehicle(robberVehicle, VehicleSeat.RightRear);
            robbers[3].SetIntoVehicle(robberVehicle, VehicleSeat.LeftRear);

            robbers[0].Task.VehicleChase(guard1); //, bankTruck.Position, 200f, 100f
        }

        public async Task OnTick()
        {
            Random randomTick = new Random();
            tickTimer += Game.LastFrameTime;
            if (tickTimer >= tickInterval)
            {

                if (awaitContact)
                {
                    await BaseScript.Delay(750);
                    float distance = Game.PlayerPed.Position.DistanceTo(bankTruck.Position);
                    if (distance < 100f)
                    {
                        DrawSubtitle("Stay close and keep an eye on the bank truck.", 7000);
                        await BaseScript.Delay(randomTick.Next(7000, 25000));
                        awaitContact= false;
                        madeContact = true;
                    }
                }

                if (madeContact)
                {
                    await setupRobbers();
                    madeContact = false;
                }

                if (randomTick.Next(0, 100) < 3)
                {
                    robbersCantTakeTheHeat = true; //robbers leave the bank truck
                }

                if (robbersCantTakeTheHeat)
                {
                    await robbersFlee();
                }
                else
                {
                    if (awaitAttack && !madeContact && !awaitContact)
                    {
                        if (!robberVehicle.Exists())
                        {
                            await setupRobbers();
                        }

                        robberVehicle.AttachBlip();

                        await BaseScript.Delay(750);
                        float distance = robbers[0].Position.DistanceTo(bankTruck.Position);
                        if (distance < 100f)
                        {
                            guard1.Task.FleeFrom(robbers[0]);
                            for (int i = 1; i < 4; i++)
                            {
                                if (randomTick.Next(0, 100) < 50)
                                {
                                    robbers[i].Task.FightAgainst(guard1);
                                    await BaseScript.Delay(randomTick.Next(1000, 3900));
                                    robbers[i].Task.ClearAll();
                                }
                            }
                        }
                    }
                }
                
                tickTimer = 0f;
            }

        }

        public async Task robbersFlee()
        {
            Random randomTick = new Random();
            robbers[0].Task.FleeFrom(Game.PlayerPed);
            await BaseScript.Delay(750);
            float distance = robbers[0].Position.DistanceTo(Game.PlayerPed.Position);
            if (distance < 200f)
            {
                guard1.Task.FleeFrom(robbers[0]);
                for (int i = 1; i < 4; i++)
                {
                    if (randomTick.Next(0, 100) < 50)
                    {
                        robbers[i].Task.FightAgainst(Game.PlayerPed);
                        await BaseScript.Delay(randomTick.Next(1000, 3900));
                        robbers[i].Task.ClearAll();
                    }
                }
            }
        } 

        private void Notify(string message)
        {
            ShowNetworkedNotification(message, "CHAR_CALL911", "CHAR_CALL911", "Dispatch", "AIR-1", 15f);
        }
        private void DrawSubtitle(string message, int duration)
        {
            API.BeginTextCommandPrint("STRING");
            API.AddTextComponentSubstringPlayerName(message);
            API.EndTextCommandPrint(duration, false);
        }
    }
}

