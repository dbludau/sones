﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using sones.GraphDB;
using sones.GraphQL;
using sones.GraphQL.Result;
using sones.Library.Commons.Security;
using sones.Library.Commons.Transaction;
using sones.Library.ErrorHandling;
using sones.Plugins.SonesGQL.DBImport.ErrorHandling;

namespace sones.Plugins.SonesGQL.DBImport
{
    public sealed class GraphDBImport_GQL : IGraphDBImport
    {

        public GraphDBImport_GQL()
        { }

        #region IGraphDBImport Members

        string IGraphDBImport.ImportFormat
        {
            get { return "GQL"; }
        }

        public QueryResult Import(String location, IGraphDB myGraphDB, IGraphQL myGraphQL, SecurityToken mySecurityToken, TransactionToken myTransactionToken, bool myBreakOnError = false, UInt32 parallelTasks = 1, IEnumerable<string> comments = null, UInt64? offset = null, UInt64? limit = null)
        {
            ASonesException error;
            Stream stream = null;
            QueryResult result;

            #region Read querie lines from location

            try
            {
                if (location.ToLower().StartsWith(@"file:\\"))
                {
                    //lines = ReadFile(location.Substring(@"file:\\".Length));
                    stream = GetStreamFromFile(location.Substring(@"file:\\".Length));
                }
                else if (location.ToLower().StartsWith("http://"))
                {
                    stream = GetStreamFromHttp(location);
                }
                else
                {
                    error = new InvalidImportLocationException(location, @"file:\\", "http://");
                    result = new QueryResult("", "GQL", 0L, ResultType.Failed, null, error);
                    return result;
                }

                #region Start import using the AGraphDBImport implementation and return the result

                return Import(stream, myGraphDB, myGraphQL, mySecurityToken, myTransactionToken, myBreakOnError, parallelTasks, comments, offset, limit);

                #endregion
            }
            catch (Exception ex)
            {
                error = new ImportFailedException(ex);
                result = new QueryResult("", "GQL", 0L, ResultType.Failed, null, error);
                return result;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
            }

            #endregion
        }
        
        public QueryResult Import(Stream myInputStream, IGraphDB myIGraphDB, IGraphQL myGraphQL, SecurityToken mySecurityToken, TransactionToken myTransactionToken, bool myBreakOnError = false, UInt32 myParallelTasks = 1, IEnumerable<string> myComments = null, ulong? myOffset = null, ulong? myLimit = null)
        {
            var lines = ReadLinesFromStream(myInputStream);

            #region Evaluate Limit and Offset

            if (myOffset != null)
            {
                throw new NotImplementedException();
                //lines = lines.SkipULong(myOffset.Value);
            }
            if (myLimit != null)
            {
                throw new NotImplementedException();
                //lines = lines.TakeULong(myLimit.Value);
            }

            #endregion

            #region Import queries

            //if (myParallelTasks > 1)
            //{
            //    queryResult = ExecuteAsParallel(lines, myIGraphDB, gqlQuery, myParallelTasks, myComments);
            //}
            //else
            //{
            var queryResult = ExecuteAsSingleThread(lines, myGraphQL, mySecurityToken, myTransactionToken, myBreakOnError, myComments);
            //}

            #endregion

            return queryResult;
        }

        

        //private QueryResult ExecuteAsParallel(IEnumerable<String> myLines, IGraphDB myIGraphDB, GraphQLQuery myGQLQuery, UInt32 parallelTasks = 1, IEnumerable<String> comments = null)
        //{

        //    var queryResult = new QueryResult();
        //    var aggregatedResults = new List<IEnumerable<IVertex>>();

        //    #region Create parallel options

        //    var parallelOptions = new ParallelOptions()
        //    {
        //        MaxDegreeOfParallelism = (int)parallelTasks
        //    };

        //    #endregion

        //    Int64 numberOfLine = 0;

        //    Parallel.ForEach(myLines, parallelOptions, (line, state) =>
        //    {

        //        if (!IsComment(line, comments))
        //        {

        //            Interlocked.Add(ref numberOfLine, 1L);

        //            if (!IsComment(line, comments)) // Skip comments
        //            {

        //                var qresult = ExecuteQuery(line, myIGraphDBSession, myGQLQuery);

        //                #region VerbosityTypes.Full: Add result

        //                if (verbosityTypes == VerbosityTypes.Full)
        //                {
        //                    lock (aggregatedResults)
        //                    {
        //                        aggregatedResults.Add(qresult.Vertices);
        //                    }
        //                }

        //                #endregion

        //                #region !VerbosityTypes.Silent: Add errors and break execution

        //                if (qresult.ResultType != ResultType.Successful && verbosityTypes != VerbosityTypes.Silent)
        //                {
        //                    lock (queryResult)
        //                    {
        //                        queryResult.PushIErrors(new[] { new Errors.Error_ImportFailed(line, numberOfLine) });
        //                        queryResult.PushIErrors(qresult.Errors);
        //                        queryResult.PushIWarnings(qresult.Warnings);
        //                    }
        //                    state.Break();
        //                }

        //                #endregion

        //            }

        //        }

        //    });


        //    //add the results of each query into the queryResult
        //    queryResult.Vertices = AggregateListOfListOfVertices(aggregatedResults);

        //    return queryResult;

        //}

        private QueryResult ExecuteAsSingleThread(IEnumerable<String> myLines, IGraphQL myIGraphQL, SecurityToken mySecurityToken, TransactionToken myTransactionToken, bool myBreakOnError = false, IEnumerable<String> comments = null)
        {

            QueryResult queryResult = new QueryResult("", "GQL", 0L, ResultType.Failed);
            ASonesException error;
            Int64 numberOfLine = 0;
            var query = String.Empty;
            var aggregatedResults = new List<IEnumerable<IVertexView>>();

            foreach (var _Line in myLines)
            {
                numberOfLine++;

                if (String.IsNullOrWhiteSpace(_Line))
                {
                    continue;
                }

                #region Skip comments

                if (IsComment(_Line, comments))
                {
                    continue;
                }

                #endregion

                query += _Line;

                var tempResult = ExecuteQuery(query, myIGraphQL, mySecurityToken, myTransactionToken);

                aggregatedResults.Add(tempResult.Vertices);
                
                #region Add errors and break execution

                if (tempResult.TypeOfResult == ResultType.Failed)
                {
                    if (tempResult.Error.Message.Equals("Mal-formed  string literal - cannot find termination symbol."))
                    {
                        Debug.WriteLine("Query at line [" + numberOfLine + "] [" + query + "] failed with " + tempResult.Error.ToString() + " add next line...");
                    }

                    if (myBreakOnError)
                    {
                        queryResult = tempResult;

                        break;
                    }
                }
                
                #endregion

                query = String.Empty;
                queryResult = tempResult;
            }

            //add the results of each query into the queryResult
            queryResult.Vertices = AggregateListOfListOfVertices(aggregatedResults);

            return queryResult;

        }

        /// <summary>
        /// Aggregates different enumerations of readout objects
        /// </summary>
        /// <param name="myListOfListOfVertices"></param>
        /// <returns></returns>
        private IEnumerable<IVertexView> AggregateListOfListOfVertices(List<IEnumerable<IVertexView>> myListOfListOfVertices)
        {
            foreach (var _ListOfVertices in myListOfListOfVertices)
            {
                foreach (var _Vertex in _ListOfVertices)
                {
                    yield return _Vertex;
                }
            }

            yield break;
        }

        private Boolean IsComment(String myQuery, IEnumerable<String> comments = null)
        {
            if (comments == null || comments.Count() == 0)
                return false;

            return comments.Any(c => myQuery.StartsWith(c));
        }

        private QueryResult ExecuteQuery(String myQuery, IGraphQL myGraphQL, SecurityToken mySecurityToken, TransactionToken myTransactionToken)
        {
            return myGraphQL.Query(mySecurityToken, myTransactionToken, myQuery);
        }

        #endregion

        #region Get streams

        /// <summary>
        /// Reads a file, just let all exceptions thrown, they are too much to pack them into a graphDBException.
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        private Stream GetStreamFromFile(String location)
        {
            return File.OpenRead(location);
        }

        /// <summary>
        /// Reads a http ressource, just let all exceptions thrown, they are too much to pack them into a graphDBException.
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        private Stream GetStreamFromHttp(String location)
        {
            var request = (HttpWebRequest)WebRequest.Create(location);
            var response = request.GetResponse();
            return response.GetResponseStream();
        }

        #endregion

        #region protected methods

        /// <summary>
        /// Will read all lines of the <paramref name="myInputStream"/>. 
        /// Keep in mind, that this can be done ony 1 time. You need to seek the stream to 0 to reread all lines again.
        /// </summary>
        /// <param name="myInputStream"></param>
        /// <returns></returns>
        private IEnumerable<String> ReadLinesFromStream(Stream myInputStream)
        {
            var streamReader = new StreamReader(myInputStream);
            while (!streamReader.EndOfStream)
            {
                yield return streamReader.ReadLine();
            }
        }

        #endregion

        #region IPluginable Members

        public string PluginName
        {
            get { return "GQLIMPORT"; }
        }

        public Dictionary<string, Type> SetableParameters
        {
            get { return new Dictionary<string, Type>(); }
        }

        public Library.VersionedPluginManager.IPluginable InitializePlugin(Dictionary<string, object> myParameters = null)
        {
            return new GraphDBImport_GQL();
        }

        #endregion
    }
}