////using UnityEngine;
////using SQLite;
////using System.IO;
////using UnityEngine.Android;
////using System;
////using System.Collections.Generic;
////using System.Collections;
////using UnityEngine.Networking;
////using Newtonsoft.Json;
////using System.Linq;




////public class DatabaseManager : MonoBehaviour
////{
////    public static DatabaseManager Instance { get; private set; }

////    private SQLiteConnection _connection;
////    private string _dbPath;

////    // private string _sqlInitFile = "DatabaseInit.sql"; // File chứa lệnh SQL để tạo database
////  /*  private void Awake()
////    {
////        Debug.Log("🔄 Đang khởi tạo DatabaseManager...");

////        Debug.Log($"SQLite version: {SQLite3.LibVersionNumber()}");
////        // Đảm bảo chỉ có một instance
////        if (Instance == null)
////        {
////            Instance = this;
////            DontDestroyOnLoad(gameObject);
////        }
////        else
////        {
////            Destroy(gameObject);
////            return;
////        }
////        // Xác định đường dẫn Database
////        string dbFileName = "PlayersSQLite.db";

////#if UNITY_EDITOR
////        _dbPath = Path.Combine(Application.dataPath, "Database", dbFileName);
////#elif UNITY_ANDROID || UNITY_IOS
////        _dbPath = Path.Combine(Application.persistentDataPath, dbFileName);
////#else
////        _dbPath = Path.Combine(Application.dataPath, dbFileName);
////#endif

////        Debug.Log($"📁 set Path: {_dbPath}");


////        // Kiểm tra quyền truy cập (chỉ trên Android)
////        RequestPermissions();
////        File.Delete(_dbPath);
////        // Debug.Log("🚀 Tạo database mới");
////        CreateDatabase();
////    }*/
////    private void CreateDatabase()
////    {
////        try
////        {

////            if (!File.Exists(_dbPath))
////            {
////                using (var db = new SQLiteConnection(_dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex))
////                {
////                    db.CreateTable<PlayerModel>(); // Tạo bảng Player
////                    db.CreateTable<Item>();
////                    db.CreateTable<EffectPlayer>();
////                    Debug.Log("✅ Database   đã được tạo.");
////                }
////                //Debug.Log($"🗑 Xóa database cũ: {_dbPath}");
////                //File.Delete(_dbPath);
////            }
////        }
////        catch (SQLiteException ex)
////        {
////            Debug.LogError($"❌ Lỗi tạo database: {ex.Message}");
////        }
////    }









////    private void OnApplicationQuit()
////    {
////        CloseConnection();
////    }


////    // 📌 Đóng kết nối SQLite
////    public void CloseConnection()
////    {
////        if (_connection != null)
////        {
////            _connection.Close();
////            _connection.Dispose();
////            _connection = null;
////            Debug.Log("🔒 Đã đóng kết nối SQLite.");
////        }
////    }





////    // 📌 Kiểm tra và yêu cầu quyền (chỉ trên Android)
////    private void RequestPermissions()
////    {
////#if UNITY_ANDROID
////        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
////            Permission.RequestUserPermission(Permission.ExternalStorageWrite);

////        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead))
////            Permission.RequestUserPermission(Permission.ExternalStorageRead);
////#endif
////    }

////    public PlayerModel GetPlayerData()
////    {
////        try
////        {
////            using (var db = new SQLiteConnection(_dbPath))
////            {
////                return db.Table<PlayerModel>().FirstOrDefault(); // Lấy player đầu tiên (hoặc null nếu không có)
////            }
////        }
////        catch (Exception ex)
////        {
////            Debug.LogError($"❌ [SQLite] Lỗi khi lấy dữ liệu: {ex.Message}");
////            return null;
////        }
////    }
////    public PlayerModelDetail GetPlayerDataServer(string id)
////    {

////        // Tạo yêu cầu GET
////        UnityWebRequest request = new UnityWebRequest(ApiConfig.BaseUrl + "player/" + id, "GET");
////        request.downloadHandler = new DownloadHandlerBuffer();  // Đảm bảo download dữ liệu về

////        // Gửi yêu cầu và đợi phản hồi
////        request.SendWebRequest();

////        // Kiểm tra kết quả
////        if (request.result == UnityWebRequest.Result.Success)
////        {
////            // Parse JSON response từ server
////            string jsonResponse = request.downloadHandler.text;
          

////            // Giả sử dữ liệu trả về là một danh sách người chơi (PlayerModel)
////            var playerData = JsonConvert.DeserializeObject<PlayerModelDetail>(jsonResponse);
////            Debug.Log("Data received: " + playerData);
////            return playerData;
////        }
////        else
////        {
////            //Debug.LogError("❌ Lỗi khi gửi yêu cầu: " + request.error);
////            return null;
////        }

////    }
////    // Kiểm tra xem có tài khoản không
////    public bool IsHasAccount()
////    {
////        try
////        {
////            using (var db = new SQLiteConnection(_dbPath))
////            {
////                return db.GetTableInfo("Player").Count > 0;
////            }
////        }
////        catch (Exception ex)
////        {
////            Debug.LogError($"❌ Lỗi SQLite IsHasAccount: {ex}");
////            return false;
////        }
////    }


////    public List<Item> GetListItemFilter(int TypeItem, int Location, bool isOpen)
////    {
////        try
////        {
////            var lstResult = new List<Item>();
////            using (var db = new SQLiteConnection(_dbPath))
////            {
////                lstResult = db.Table<Item>()
////    .Where(item => ((item.LocationGid == Location && item.isOpen == isOpen) || item.ID == ((int)ItemCode.CuliRing)) && (TypeItem == (int)TypeItemGid.All || item.TypeGid == TypeItem)) // Lọc các item bán trong shop
////    .OrderBy(item => item.ID) // Sắp xếp theo ID
////    .ToList();
////                return lstResult;
////            }

////        }
////        catch (Exception ex)
////        {
////            Debug.LogError($"❌ [SQLite] Lỗi khi lấy danh sách Item: {ex.Message}");
////            return new List<Item>();
////        }
////    }
////    public List<EffectPlayer> GetListEffectFilter()
////    {
////        try
////        {
////            var lstResult = new List<EffectPlayer>();
////            using (var db = new SQLiteConnection(_dbPath))
////            {
////                lstResult = db.Table<EffectPlayer>()
////    .OrderBy(item => item.ID) // Sắp xếp theo ID
////    .ToList();
////                return lstResult;
////            }

////        }
////        catch (Exception ex)
////        {
////            throw new Exception(ex.Message.ToString());
////        }
////    }

////    public Item GetItemById(int itemId)
////    {
////        try
////        {
////            using (var db = new SQLiteConnection(_dbPath))
////                return db.Find<Item>(itemId);
////        }
////        catch (Exception ex)
////        {
////            Debug.LogError($"[SQLite] GetItemById failed: {ex.Message}");
////            return null;
////        }
////    }
////    public int UpdateEffectByID(int ID)
////    {
////        try
////        {
////            int result= 0;
////            using (var db = new SQLiteConnection(_dbPath))
////            {
////                var item = db.Find<EffectPlayer>(ID);
////                var player = db.Table<PlayerModel>().FirstOrDefault();
                
////                if (item != null && player != null)
////                {
////                    if(player.TalentPoint == 0)
////                    {
////                        return -1;
////                    }   
////                    item.Level += 1;
////                    if (!item.IsPassive)
////                        item.Charges += 1;
////                    player.TalentPoint -=  1;
////                    result = player.TalentPoint;
////                    db.RunInTransaction(() =>
////                    {
////                        db.Update(item);
////                        db.Update(player);
////                    });
////                }
////            }
////            return result;
////        }
////        catch (Exception ex)
////        {
////            throw new Exception(ex.Message.ToString());
////        }
////    } 
////        // Tạo người chơi mới
////        public void CreateUser(string playerName)
////    {
////        try
////        {
////            PlayerModel newPlayer = new PlayerModel
////            {
////                PlayerName = playerName,
////                Level = 1,
////                Exp = 0,
////                TalentPoint = 10,
////                Ball = (int)ItemCode.CuliBaKhiaVang,
////                Shirt = (int)ItemCode.AoThunBasic,
////                Pant = 0,
////                RingBall = 100,
////                Money = 3000,
////                Hair = (int)ItemCode.TocDaiLangTu
////            };
////            var lstItem = new List<Item>();
////            #region [========== Đồ tân thủ==============]
////            Item newItem1 = new Item
////            {
////                ID = (int)ItemCode.CuliBaKhiaVang,
////                Name = "Cu li ba khía vàng",
////                Level = 1,
////                LocationGid = (int)LocationItemGid.Inventory,
////                TypeGid = (int)TypeItemGid.Culi,
////                Description = "Culi ba khía vàng tặng cho bạn",
////                isOpen = true,
////                IsLevelUp = true,
////                Price = 0

////            };
////            lstItem.Add(newItem1);

////            Item newItem3 = new Item
////            {
////                ID = (int)ItemCode.AoThunBasic,
////                Name = "Áo thun",
////                Level = 0,
////                LocationGid = (int)LocationItemGid.Inventory,
////                TypeGid = (int)TypeItemGid.Clother,
////                Description = "Áo bình thường mẹ mua cho",
////                isOpen = true,
////                IsLevelUp = false,
////                Price = 0

////            };
////            lstItem.Add(newItem3);

////            Item newItem9 = new Item
////            {
////                ID = (int)ItemCode.TocChuanMen,
////                Name = "Tóc chuẩn men",
////                Level = 0,
////                LocationGid = (int)LocationItemGid.Inventory,
////                TypeGid = (int)TypeItemGid.Clother,
////                Description = "Đi hớt tóc mẹ kêu hớt cao lên",
////                isOpen = true,
////                IsLevelUp = false,
////                Price = 0

////            };
////            lstItem.Add(newItem9);
////            #endregion

////            #region[============== Cửa hàng Tóc==============]
////            Item newItem2 = new Item
////            {
////                ID = (int)ItemCode.TocDaiLangTu,
////                Name = "Tóc Dài lãng tử",
////                Level = 0,
////                LocationGid = (int)LocationItemGid.Shop,
////                TypeGid = (int)TypeItemGid.Hair,
////                Description = "Tóc để lâu ngày chưa hớt",
////                isOpen = false,
////                IsLevelUp = false,
////                Price = 1800

////            };
////            lstItem.Add(newItem2);
////            #endregion

////            #region[============== Cửa hàng Culi==============]
////            Item newItem7 = new Item
////            {
////                ID = (int)ItemCode.CuliSuaDen,
////                Name = "CuLi Sữa đen",
////                Level = 1,
////                LocationGid = (int)LocationItemGid.Shop,
////                TypeGid = (int)TypeItemGid.Culi,
////                Description = "CuLi sữa đặc mạnh mẽ",
////                isOpen = false,
////                IsLevelUp = true,
////                Price = 700

////            };
////            lstItem.Add(newItem7);
////            #endregion

////            #region[============== Cửa hàng Quần áo==============]
////            Item newItem4 = new Item
////            {
////                ID = (int)ItemCode.AoBanhCam,
////                Name = "Áo bánh cam",
////                Level = 0,
////                LocationGid = (int)LocationItemGid.Shop,
////                TypeGid = (int)TypeItemGid.Clother,
////                Description = "Chiếc áo làm từ bánh cam. bạn có tin không ?",
////                isOpen = false,
////                IsLevelUp = false,
////                Price = 2500

////            };
////            lstItem.Add(newItem4);
////            #endregion


////            #region[============== Cửa hàng Khác==============]
////            Item newItem8 = new Item
////            {
////                ID = (int)ItemCode.CuliRing,
////                Name = "CulLi đậu vòng",
////                Level = 0,
////                LocationGid = (int)LocationItemGid.Inventory,
////                TypeGid = (int)TypeItemGid.Other,
////                Description = "CuLi dùng để đậu vòng. 500 đồng/ 10 cục",
////                isOpen = true,
////                IsLevelUp = false,
////                Price = 500

////            };
////            lstItem.Add(newItem8);
////            #endregion

////            #region[============== VIP==============]
////            Item newItem5 = new Item
////            {
////                ID = (int)ItemCode.CuliMatTroi,
////                Name = "Culi Lửa Mặt Trời",
////                Level = 1,
////                LocationGid = (int)LocationItemGid.Shop,
////                TypeGid = (int)TypeItemGid.Culi,
////                Description = "Culi được phơi dưới ánh nắng mặt trời hấp thụ linh khí từ thần mặt trời tạo nên tia lửa mạnh mẽ.",
////                isOpen = false,
////                IsLevelUp = true,
////                Price = 1500

////            };
////            lstItem.Add(newItem5);

////            Item newItem6 = new Item
////            {
////                ID = (int)ItemCode.CuliBangGia,
////                Name = "CuLi Băng giá",
////                Level = 1,
////                LocationGid = (int)LocationItemGid.Shop,
////                TypeGid = (int)TypeItemGid.Culi,
////                Description = "Cu li được tạo ra từ nước mưa, bắn ra những làn sóng tia mát",
////                isOpen = false,
////                IsLevelUp = true,
////                Price = 1500

////            };
////            lstItem.Add(newItem6);
////            #endregion

 

////            //Kỹ năng
////            var lsteffect = new List<EffectPlayer>();
////            var effect = new EffectPlayer
////            {
////                ID=(int)EffectPlayerType.PowerSkill,
////                Name = "name_power",
////                Power = 1,
////                Spin = 0,
////                Mentality = 0,
////                Level = 0,
////                Charges = 0,
////                ParentId = (int)EffectPlayerType.ChamCat,
////                IsPassive = true,
////                Description = "description_power"
////            };
////            lsteffect.Add(effect);
////            //var effect2 = new EffectPlayer
////            //{
////            //    ID = (int)EffectPlayerType.PowerSkill_lv2,
////            //    Name = "name_power",
////            //    Power = 1,
////            //    Spin = 0,
////            //    Mentality = 0,
////            //    Level = 0,
////            //    Charges = 0,
////            //    ParentId = (int)EffectPlayerType.PowerSkill_lv1,
////            //    Description = "description_power"
////            //};
////            //lsteffect.Add(effect2);
////            //var effect3 = new EffectPlayer
////            //{
////            //    ID = (int)EffectPlayerType.PowerSkill_lv3,
////            //    Name = "name_power",
////            //    Power = 1,
////            //    Spin = 0,
////            //    Mentality = 0,
////            //    Level = 0,
////            //    Charges = 0,
////            //    ParentId = (int)EffectPlayerType.PowerSkill_lv2,
////            //    Description = "description_power"
////            //};
////            //lsteffect.Add(effect3);
////            var effect4 = new EffectPlayer
////            {
////                ID = (int)EffectPlayerType.SpinSkill,
////                Name = "name_spin",
////                Power = 0,
////                Spin = 2,
////                Mentality = 0,
////                Level = 0,
////                Charges = 0,
////                ParentId = (int)EffectPlayerType.ChamCat,
////                IsPassive = true,
////                Description = "description_spin"
////            };
////            lsteffect.Add(effect4);

////            //var effect5 = new EffectPlayer
////            //{
////            //    ID = (int)EffectPlayerType.SpinSkill_lv2,
////            //    Name = "name_spin",
////            //    Power = 0,
////            //    Spin = 2,
////            //    Mentality = 0,
////            //    Level = 0,
////            //    Charges = 0,
////            //    ParentId = (int)EffectPlayerType.SpinSkill_lv1,
////            //    Description = "description_spin"
////            //};
////            //lsteffect.Add(effect5);

////            //var effect6 = new EffectPlayer
////            //{
////            //    ID = (int)EffectPlayerType.SpinSkill_lv3,
////            //    Name = "name_spin",
////            //    Power = 0,
////            //    Spin = 2,
////            //    Mentality = 0,
////            //    Level = 0,
////            //    Charges = 0,
////            //    ParentId = (int)EffectPlayerType.SpinSkill_lv2,
////            //    Description = "description_spin"
////            //};
////            //lsteffect.Add(effect6);

////            var effect7 = new EffectPlayer
////            {
////                ID = (int)EffectPlayerType.MentalitySkill,
////                Name = "name_mentality",
////                Power = 0,
////                Spin = 0,
////                Mentality = 1,
////                Level = 0,
////                Charges = 0,
////                ParentId = (int)EffectPlayerType.ChamCat,
////                IsPassive = true,
////                Description = "description_mentality"
////            };
////            lsteffect.Add(effect7);

////            //var effect8 = new EffectPlayer
////            //{
////            //    ID = (int)EffectPlayerType.MentalitySkill_lv2,
////            //    Name = "name_mentality",
////            //    Power = 0,
////            //    Spin = 0,
////            //    Mentality = 1,
////            //    Level = 0,
////            //    Charges = 0,
////            //    ParentId = (int)EffectPlayerType.MentalitySkill_lv1,
////            //    Description = "description_mentality"
////            //};
////            //lsteffect.Add(effect8);


////            //var effect9 = new EffectPlayer
////            //{
////            //    ID = (int)EffectPlayerType.MentalitySkill_lv3,
////            //    Name = "name_mentality",
////            //    Power = 0,
////            //    Spin = 0,
////            //    Mentality = 1,
////            //    Level = 0,
////            //    Charges = 0,
////            //    ParentId = (int)EffectPlayerType.MentalitySkill_lv2,
////            //    Description = "description_mentality"
////            //};
////            //lsteffect.Add(effect9);

////            var effect10 = new EffectPlayer
////            {
////                ID = (int)EffectPlayerType.HachDichSkill,
////                Name = "name_hacdich",
////                Power = 0,
////                Spin = 0,
////                Mentality = 0,
////                Level = 0,
////                Charges = 0,
////                IsPassive = false,
////                ParentId = (int)EffectPlayerType.PowerSkill,
////                Description = "description_hachdich"
////            };
////            lsteffect.Add(effect10);

////            //var effect11 = new EffectPlayer
////            //{
////            //    ID = (int)EffectPlayerType.HachDichSkill_lv2,
////            //    Name = "name_hacdich",
////            //    Power = 0,
////            //    Spin = 0,
////            //    Mentality = 0,
////            //    Level = 0,
////            //    Charges = 0,
////            //    ParentId = (int)EffectPlayerType.HachDichSkill_lv1,
////            //    Description = "description_hachdich"
////            //};
////            //lsteffect.Add(effect11);

////            //var effect12 = new EffectPlayer
////            //{
////            //    ID = (int)EffectPlayerType.ViewSkill_lv1,
////            //    Name = "name_view",
////            //    Power = 0,
////            //    Spin = 0,
////            //    Mentality = 0,
////            //    Level = 0,
////            //    Charges = 0,
////            //    ParentId = (int)EffectPlayerType.MentalitySkill_lv3,
////            //    Description = "description_view"
////            //};
////            //lsteffect.Add(effect12);


////            var effect13 = new EffectPlayer
////            {
////                ID = (int)EffectPlayerType.ViewSkill,
////                Name = "name_view",
////                Power = 0,
////                Spin = 0,
////                Mentality = 0,
////                Level = 0,
////                Charges = 1,
////                ParentId = (int)EffectPlayerType.MentalitySkill,
////                IsPassive = false,
////                Description = "description_view"
////            };
////            lsteffect.Add(effect13);


////            var effect14 = new EffectPlayer
////            {
////                ID = (int)EffectPlayerType.ChiemSkill,
////                Name = "name_chiem",
////                Power = 0,
////                Spin = 0,
////                Mentality = 0,
////                Level = 0,
////                Charges = 0,
////                IsPassive = true,
////                ParentId = (int)EffectPlayerType.ChamCat,
////                Description = "description_chiem"
////            };
////            lsteffect.Add(effect14);

////            //var effect15 = new EffectPlayer
////            //{
////            //    ID = (int)EffectPlayerType.ChiemSkill_lv2,
////            //    Name = "name_chiem",
////            //    Power = 0,
////            //    Spin = 0,
////            //    Mentality = 0,
////            //    Level = 0,
////            //    Charges = 0,
////            //    ParentId = (int)EffectPlayerType.ChiemSkill_lv1,
////            //    Description = "description_chiem"
////            //};
////            //lsteffect.Add(effect15);


////            //var effect16 = new EffectPlayer
////            //{
////            //    ID = (int)EffectPlayerType.CatAnTienSkill_lv1,
////            //    Name = "name_catantien",
////            //    Power = 0,
////            //    Spin = 0,
////            //    Mentality = 0,
////            //    Level = 0,
////            //    Charges = 0,
////            //    ParentId = (int)EffectPlayerType.PowerSkill_lv3,
////            //    Description = "description_catantien"
////            //};
////            //lsteffect.Add(effect16);

////            var effect17 = new EffectPlayer
////            {
////                ID = (int)EffectPlayerType.CatAnTienSkill,
////                Name = "name_catantien",
////                Power = 0,
////                Spin = 0,
////                Mentality = 0,
////                Level = 0,
////                Charges = 0,
////                ParentId = 0,
////                IsPassive = false,
////                Description = "description_catantien"
////            };
////            lsteffect.Add(effect17);


////            var effect18 = new EffectPlayer
////            {
////                ID = (int)EffectPlayerType.ChamCat,
////                Name = "name_chamcat",
////                Power = 0,
////                Spin = 0,
////                Mentality = 0,
////                Level = 3,
////                Charges = 6,
////                ParentId = 0,
////                IsPassive = false,
////                Description = "description_chamcat"
////            };
////            lsteffect.Add(effect18);

////            var effect19 = new EffectPlayer
////            {
////                ID = (int)EffectPlayerType.BigBallSkill,
////                Name = "name_bigball",
////                Power = 0,
////                Spin = 0,
////                Mentality = 0,
////                Level = 0,
////                Charges = 1,
////                ParentId = 0,
////                IsPassive = false,
////                Description = "description_bigball"
////            };
////            lsteffect.Add(effect19);

////            var effect20 = new EffectPlayer
////            {
////                ID = (int)EffectPlayerType.WindBlowSkill,
////                Name = "name_windblow",
////                Power = 0,
////                Spin = 0,
////                Mentality = 0,
////                Level = 0,
////                Charges = 1,
////                ParentId = 0,
////                IsPassive = false,
////                Description = "description_windblow"
////            };
////            lsteffect.Add(effect20);

////            using (var db = new SQLiteConnection(_dbPath))
////            {
////                db.Insert(newPlayer);
////                db.InsertAll(lstItem);
////                db.InsertAll(lsteffect);
////            }    
////            Debug.Log($"✅ [SQLite] Tạo user mới: {playerName}");
////        }
////        catch (Exception ex)
////        {
////            Debug.LogError($"❌ [SQLite] Lỗi khi tạo user: {ex.Message}");
////        }
////    }
////    // ✅ 1. Nâng cấp level của Item
////    public void UpgradeLevelForItem(int idItem)
////    {
////        try
////        {
////            var item = _connection.Find<ItemBall>(idItem);
////            if (item != null)
////            {
////                item.Level += 1;
////                _connection.Update(item);
////                Debug.Log($"✅ [SQLite] Cấp độ item {idItem} đã được tăng lên {item.Level}");
////            }
////        }
////        catch (Exception ex)
////        {
////            Debug.LogError($"❌ [SQLite] Lỗi khi nâng cấp item: {ex.Message}");
////        }
////    }
////    public void BuyItem(int idItem, int money, Action callback)
////    {
////        try
////        {
////            using (var db = new SQLiteConnection(_dbPath))
////            {
////                var item = db.Find<Item>(idItem);
////                var player = db.Table<PlayerModel>().FirstOrDefault();

////                if (item != null && player != null)
////                {
////                    item.Level = 0;
////                    item.LocationGid = (int)LocationItemGid.Inventory; // đổi vị trí
////                    item.isOpen = true; // Đánh dấu item đã mua
////                    if (item.ID == (int)ItemCode.CuliRing)
////                         player.RingBall += 10;
////                    player.Money -= money; // trừ tiền cho người chơi

////                    db.RunInTransaction(() =>
////                    {
////                        db.Update(item);
////                        db.Update(player);
////                    });
////                }
////            }

////        }
////        catch (Exception ex)
////        {
////            Debug.LogError($"❌ [SQLite] Lỗi khi bán item: {ex.Message}");
////        }

////        callback?.Invoke();
////    }
////    // ✅ 2. Bán Item
////    public void SoldItem(int idItem, int money, Action callback)
////    {
////        try
////        {
////            using (var db = new SQLiteConnection(_dbPath))
////            {
////                var item = db.Find<Item>(idItem);
////                var player = db.Table<PlayerModel>().FirstOrDefault();

////                if (item != null && player != null)
////                {
////                    if (item.ID == (int)ItemCode.CuliRing)
////                    {
////                        if (player.RingBall <= 0)
////                            return;
////                        player.RingBall -= 10;
////                    }
////                    else
////                    {
////                        item.Level = 0;
////                        item.LocationGid = (int)LocationItemGid.Shop;
////                        item.isOpen = false; // Đánh dấu item đã bán
////                    }
////                    player.Money += money; // Cộng tiền cho người chơi

////                    db.RunInTransaction(() =>
////                    {
////                        db.Update(item);
////                        db.Update(player);
////                    });
////                }
////            }    
              
////        }
////        catch (Exception ex)
////        {
////            Debug.LogError($"❌ [SQLite] Lỗi khi bán item: {ex.Message}");
////        }

////        callback?.Invoke();
////    }

////    // ✅ 3. Cập nhật trang bị của người chơi
////    public void UpdateEquipBall(int idItem, int typeGid, Action callback)
////    {
////        try
////        {
////            using (var db = new SQLiteConnection(_dbPath))
////            {
////                var item = db.Find<Item>(idItem);
////                var itemFix = db.Find<Item>(idItem);
////                var player = db.Table<PlayerModel>().FirstOrDefault();
////                if (item != null && player != null)
////                {
////                    item.LocationGid = (int)LocationItemGid.Equipped;
////                    switch (typeGid)
////                    {
////                        case 1:
////                            itemFix = db.Find<Item>(player.Ball);
////                            itemFix.LocationGid = (int)LocationItemGid.Inventory;
////                            player.Ball = idItem;
////                            break;
////                        case 2:
////                            itemFix = db.Find<Item>(player.Shirt);
////                            itemFix.LocationGid = (int)LocationItemGid.Inventory;
////                            player.Shirt = idItem;
////                            break;
////                        case 3:
////                            itemFix = db.Find<Item>(player.Hair);
////                            itemFix.LocationGid = (int)LocationItemGid.Inventory;
////                            player.Hair = idItem;
////                            break;
////                    }

////                    db.RunInTransaction(() =>
////                    {
////                        db.Update(item);
////                        db.Update(itemFix);
////                        db.Update(player);
////                    });
////                }
////            }
////        }
////        catch (Exception ex)
////        {
////            Debug.LogError($"❌ [SQLite] Lỗi khi cập nhật trang bị: {ex.Message}");
////        }

////        callback?.Invoke();
////    }

////    // ✅ 4. Cập nhật RingBall & Exp sau khi kết thúc game
////    //public void UpdateForOverGame(int inputQty, int exp)
////    //{
////    //    try
////    //    {
////    //        using (var db = new SQLiteConnection(_dbPath))
////    //        {
////    //            var player = db.Table<PlayerModel>().FirstOrDefault();
////    //            if (player != null)
////    //            {
////    //                player.RingBall += inputQty;
////    //                player.Exp += exp;

////    //                while (player.Level <= LevelExpRequirements.Length && player.Exp >= GetExpForNextLevel(player.Level))
////    //                {
////    //                    player.Exp -= GetExpForNextLevel(player.Level);
////    //                    player.Level += 1;
////    //                }

////    //                db.Update(player);
////    //            }
////    //        }

////    //    }
////    //    catch (Exception ex)
////    //    {
////    //        Debug.LogError($"❌ [SQLite] Lỗi khi cập nhật RingBall & Exp: {ex.Message}");
////    //    }
////    //}

////    // ✅ 5. Cập nhật số lượng RingBall
////    public void UpdateRingBall(int inputQty)
////    {
////        try
////        {
////            using (var db = new SQLiteConnection(_dbPath))
////            {
////                var player = db.Table<PlayerModel>().FirstOrDefault();
////                if (player != null)
////                {
////                    player.RingBall = Mathf.Max(player.RingBall + inputQty, 0); // Đảm bảo không âm
////                    db.Update(player);
////                }
////            }

////        }
////        catch (Exception ex)
////        {
////            Debug.LogError($"❌ [SQLite] Lỗi khi cập nhật RingBall: {ex.Message}");
////        }
////    }
////}
