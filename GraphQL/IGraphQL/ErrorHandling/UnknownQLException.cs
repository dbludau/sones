﻿using System;
using sones.Library.ErrorHandling;

namespace sones.GraphQL.ErrorHandling
{    
    /// <summary>
    /// This class represents an unknown graphql exception
    /// </summary>
    public sealed class UnknownQLException : AGraphQLException
    {
        /// <summary>
        /// The exception that has been thrown
        /// </summary>
        public Exception ThrownException { get; private set; }

        public override ushort ErrorCode
        {
            get { return ErrorCodes.UnknownQLError; }
        }

        #region constructor

        /// <summary>
        /// Creates a new UnknownQL exception
        /// </summary>
        /// <param name="e"></param>
        public UnknownQLException(Exception e)
        {
            ThrownException = e;
        }

        #endregion
    }
}