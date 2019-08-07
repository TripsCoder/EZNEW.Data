﻿using EZNEW.Develop.Entity;
using EZNEW.Develop.CQuery.Translator;
using EZNEW.Framework.Serialize;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EZNEW.Framework.Extension;
using EZNEW.Develop.Command;
using EZNEW.Develop.CQuery;
using System.Linq;
using EZNEW.Framework.IoC;
using System.Data;

namespace EZNEW.Data.Config
{
    /// <summary>
    /// db manager
    /// </summary>
    public static class DataManager
    {
        /// <summary>
        /// get or set get database servers method
        /// </summary>
        public static Func<ICommand, Task<List<ServerInfo>>> GetDBServerAsync
        {
            get; set;
        }

        /// <summary>
        /// get db connection
        /// </summary>
        public static Func<ServerInfo, IDbConnection> GetDBConnection { get; set; }

        /// <summary>
        /// db engines
        /// </summary>
        internal static Dictionary<ServerType, IDbEngine> DbEngines { get; } = new Dictionary<ServerType, IDbEngine>();

        /// <summary>
        /// translators
        /// </summary>
        internal static Dictionary<ServerType, IQueryTranslator> Translators { get; } = new Dictionary<ServerType, IQueryTranslator>();

        /// <summary>
        /// data entity configs
        /// key:entity type guid
        /// </summary>
        internal static Dictionary<Guid, DataEntityConfig> DataEntityConfigs = new Dictionary<Guid, DataEntityConfig>();

        static DataManager()
        {
            ContainerManager.Container?.Register(typeof(ICommandEngine), typeof(DBCommandEngine));
        }

        #region Methods

        #region data config

        /// <summary>
        /// data config
        /// </summary>
        /// <param name="config">data config</param>
        public static void Config(DataConfig config)
        {
            if (config == null || config.Servers == null || config.Servers.Count <= 0)
            {
                return;
            }
            foreach (var serverItem in config.Servers)
            {
                if (serverItem.Value == null)
                {
                    continue;
                }
                //register dbengine
                if (!serverItem.Value.EngineFullTypeName.IsNullOrEmpty())
                {
                    IDbEngine engine = (IDbEngine)Activator.CreateInstance(Type.GetType(serverItem.Value.EngineFullTypeName));
                    RegisterDBEngine(serverItem.Key, engine);
                }
                //register query translator
                if (!serverItem.Value.QueryTranslatorFullTypeName.IsNullOrEmpty())
                {
                    IQueryTranslator translator = (IQueryTranslator)Activator.CreateInstance(Type.GetType(serverItem.Value.QueryTranslatorFullTypeName));
                    RegisterQueryTranslator(serverItem.Key, translator);
                }
            }
        }

        /// <summary>
        /// config data through json
        /// </summary>
        /// <param name="dataConfigJson">json value</param>
        public static void Config(string dataConfigJson)
        {
            if (string.IsNullOrWhiteSpace(dataConfigJson))
            {
                return;
            }
            var dataConfig = JsonSerialize.JsonToObject<DataConfig>(dataConfigJson);
            if (dataConfig == null)
            {
                return;
            }
            Config(dataConfig);
        }

        #endregion

        #region register db engine

        /// <summary>
        /// register dbengine
        /// </summary>
        /// <param name="serverType">db server type</param>
        /// <param name="dbEngine">db engine</param>
        public static void RegisterDBEngine(ServerType serverType, IDbEngine dbEngine)
        {
            if (dbEngine == null)
            {
                return;
            }
            if (DbEngines.ContainsKey(serverType))
            {
                DbEngines[serverType] = dbEngine;
            }
            else
            {
                DbEngines.Add(serverType, dbEngine);
            }
        }

        #endregion

        #region register query translator

        /// <summary>
        /// register query translator
        /// </summary>
        /// <param name="serverType">server type</param>
        /// <param name="queryTranslator">query translator</param>
        public static void RegisterQueryTranslator(ServerType serverType, IQueryTranslator queryTranslator)
        {
            if (queryTranslator == null)
            {
                return;
            }
            if (!Translators.ContainsKey(serverType))
            {
                Translators.Add(serverType, queryTranslator);
            }
            else
            {
                Translators[serverType] = queryTranslator;
            }
        }

        #endregion

        #region get entity object name

        /// <summary>
        ///get entity object name
        /// </summary>
        /// <param name="serverType">server type</param>
        /// <param name="entityType">entity type</param>
        /// <param name="searchEntityConfig">search entity config</param>
        /// <returns></returns>
        public static string GetEntityObjectName(ServerType serverType, Type entityType, string defaultName = "", bool searchEntityConfig = true)
        {
            if (entityType == null)
            {
                return string.Empty;
            }
            DataEntityConfigs.TryGetValue(entityType.GUID, out DataEntityConfig dataEntityConfig);
            EntityServerTypeConfig entityServerTypeConfig = null;
            string objectName = defaultName;
            if ((dataEntityConfig?.ServerEntitys?.TryGetValue(serverType, out entityServerTypeConfig) ?? false) && !string.IsNullOrWhiteSpace(entityServerTypeConfig?.TableName))
            {
                return entityServerTypeConfig.TableName;
            }
            if (objectName.IsNullOrEmpty() && searchEntityConfig)
            {
                objectName = EntityManager.GetEntityObjectName(entityType);
            }
            return objectName;
        }

        /// <summary>
        /// get entity object name
        /// </summary>
        /// <param name="serverType">server type</param>
        /// <param name="defaultName">default name</param>
        /// <param name="searchEntityConfig">search entity config</param>
        /// <returns></returns>
        public static string GetEntityObjectName<T>(ServerType serverType, string defaultName = "", bool searchEntityConfig = true)
        {
            return GetEntityObjectName(serverType, typeof(T), defaultName, searchEntityConfig);
        }

        #endregion

        #region get query object name

        /// <summary>
        /// get query object name
        /// </summary>
        /// <param name="serverType">server type</param>
        /// <param name="query">query object</param>
        /// <returns></returns>
        public static string GetQueryRelationObjectName(ServerType serverType, IQuery query)
        {
            if (query == null || query.EntityType == null)
            {
                return string.Empty;
            }
            return GetEntityObjectName(serverType, query.EntityType);
        }

        #endregion

        #region get field

        /// <summary>
        ///  get field
        /// </summary>
        /// <param name="serverType">db server type</param>
        /// <param name="entityType">entity type</param>
        /// <param name="propertyName">property name</param>
        /// <returns></returns>
        public static EntityField GetField(ServerType serverType, Type entityType, string propertyName)
        {
            if (entityType == null)
            {
                return propertyName;
            }
            var defaultField = EntityManager.GetField(entityType, propertyName);
            return defaultField ?? propertyName;
        }

        /// <summary>
        ///  get field
        /// </summary>
        /// <param name="serverType">db server type</param>
        /// <param name="query">query</param>
        /// <param name="propertyName">property name</param>
        /// <returns></returns>
        public static EntityField GetField(ServerType serverType, IQuery query, string propertyName)
        {
            if (query == null || query.EntityType == null)
            {
                return propertyName;
            }
            return GetField(serverType, query.EntityType, propertyName);
        }

        #endregion

        #region get fields

        /// <summary>
        /// get fields
        /// </summary>
        /// <param name="serverType">server type</param>
        /// <param name="entityType">entity type</param>
        /// <param name="propertyNames">property name</param>
        /// <returns></returns>
        public static List<EntityField> GetFields(ServerType serverType, Type entityType, IEnumerable<string> propertyNames)
        {
            if (entityType == null || propertyNames.IsNullOrEmpty())
            {
                return new List<EntityField>(0);
            }
            var defaultFields = EntityManager.GetFields(entityType, propertyNames);
            return defaultFields ?? new List<EntityField>(0);
        }

        #endregion

        #region get edit fields

        /// <summary>
        /// get edit fields
        /// </summary>
        /// <param name="entityType">entity type</param>
        /// <returns></returns>
        public static List<EntityField> GetEditFields(ServerType serverType, Type entityType)
        {
            return EntityManager.GetEntityEditFields(entityType);
        }

        #endregion

        #region get query fields

        /// <summary>
        /// get query fields
        /// </summary>
        /// <param name="entityType">entity type</param>
        /// <param name="query">query</param>
        /// <returns></returns>
        public static List<EntityField> GetQueryFields(ServerType serverType, Type entityType, IQuery query)
        {
            if (entityType == null || query == null)
            {
                return new List<EntityField>(0);
            }
            return query.GetActuallyQueryFields(entityType);
        }

        #endregion

        #region get default field

        /// <summary>
        /// get default field
        /// </summary>
        /// <param name="entityType">entity type</param>
        /// <returns></returns>
        public static EntityField GetDefaultField(ServerType serverType, Type entityType)
        {
            if (entityType == null)
            {
                return string.Empty;
            }
            var queryFields = EntityManager.GetEntityQueryFields(entityType);
            if (queryFields.IsNullOrEmpty())
            {
                return string.Empty;
            }
            return queryFields[0];
        }

        #endregion

        #endregion
    }
}
