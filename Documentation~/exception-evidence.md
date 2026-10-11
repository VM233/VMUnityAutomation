# Unexpected exception evidence

Unexpected built-in commands, workspace jobs and project tools use one evidence producer. It reports exceptionType and stackTrace from the root cause, and exceptionContext from the complete caught exception. The same original exception is logged once by its execution boundary. Domain rejections retain their declared error code and fields without acquiring an unexpected-exception label.

Transport adapters use VmAutomationInvocationResult.Failure for their own input rejection and forward the dispatch owner's result. They must not flatten a second exception envelope, replace the catalog revision with an empty value or convert an initialization exception into an ordinary command rejection. Official CLI connection/authentication/instance-discovery errors remain official transport evidence.
