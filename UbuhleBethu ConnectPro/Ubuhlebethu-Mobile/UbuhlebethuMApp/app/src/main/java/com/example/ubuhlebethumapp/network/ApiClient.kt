package com.example.ubuhlebethumapp.network

import android.content.Context
import com.example.ubuhlebethumapp.BuildConfig
import com.google.gson.Gson
import okhttp3.Authenticator
import okhttp3.Interceptor
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Response
import okhttp3.Route
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import java.util.concurrent.TimeUnit

/**
 * Singleton Retrofit holder. Base URL comes from BuildConfig so builds point at
 * the web-hosted Render API server (https://ubuhlebethuconnectproapi.onrender.com/).
 */
data class SyncAccountTag(val userId: String)
object ApiClient {

    @Volatile private var retrofit: Retrofit? = null
    @Volatile private var appContext: Context? = null
    private val refreshLock = Any()

    fun init(context: Context) {
        appContext = context.applicationContext
    }

    fun reset() {
        synchronized(this) {
            retrofit = null
        }
    }

    private fun retrofit(context: Context): Retrofit {
        retrofit?.let { return it }
        synchronized(this) {
            retrofit?.let { return it }


            val logging = HttpLoggingInterceptor().apply {
                level = if (BuildConfig.DEBUG) {
                    HttpLoggingInterceptor.Level.BASIC
                } else {
                    HttpLoggingInterceptor.Level.NONE
                }
            }



val authInterceptor = Interceptor { chain ->
    val ctx = appContext ?: context.applicationContext

    val token = TokenStore.getToken(ctx)
    val currentUserId = TokenStore.getUserId(ctx)

    val expectedUserId =
        chain.request().header("X-Sync-User-Id")

    if (!expectedUserId.isNullOrBlank() &&
        (expectedUserId != currentUserId || token.isNullOrBlank())
    ) {
        throw java.io.IOException(
            "Synchronization cancelled: account changed"
        )
    }

    val requestBuilder = chain.request().newBuilder()
        .removeHeader("X-Sync-User-Id")

    if (!token.isNullOrBlank()) {
        requestBuilder.header(
            "Authorization",
            "Bearer $token"
        )
    }

    if (!expectedUserId.isNullOrBlank()) {
        requestBuilder.tag(
            SyncAccountTag::class.java,
            SyncAccountTag(expectedUserId)
        )
    }

    chain.proceed(requestBuilder.build())
}




// Authenticator to handle 401 Unauthorized responses and refresh tokens
            val tokenAuthenticator = Authenticator { _, response ->

                // Check whether this request belongs to the active account.
                val expectedUserId =
                    response.request.tag(SyncAccountTag::class.java)?.userId

                if (expectedUserId != null &&
                    TokenStore.getUserId(context) != expectedUserId
                ) {
                    // Do not refresh using another account's credentials.
                    return@Authenticator null
                }

                // Avoid infinite refresh loops or refreshing auth endpoints.
                val path = response.request.url.encodedPath

                if (path.contains("/auth/login") || path.contains("/auth/refresh")) {
                    return@Authenticator null
                }

                if (responseCount(response) >= 2) {
                    return@Authenticator null
                }
val ctx = appContext ?: context.applicationContext
              
                synchronized(refreshLock) {
                    val currentToken = TokenStore.getToken(ctx)
                    val currentRefreshToken = TokenStore.getRefreshToken(ctx)

                    if (expectedUserId != null &&
                        TokenStore.getUserId(context) != expectedUserId
                    ) {
                        return@Authenticator null
                    }

                    if (currentRefreshToken.isNullOrBlank() || currentToken.isNullOrBlank()) {
                        return@Authenticator null
                    }


// Check whether another request refreshed the token.
val requestAuthHeader =
    response.request.header("Authorization")

if (requestAuthHeader != null &&
    requestAuthHeader != "Bearer $currentToken"
) {
    val originalToken =
        requestAuthHeader.removePrefix("Bearer ")

    val originalUserId =
        TokenStore.getUserIdFromToken(originalToken)

    val currentUserId =
        TokenStore.getUserId(ctx)

    // Never substitute another user's credentials.
    if (originalUserId.isNullOrBlank() ||
        originalUserId != currentUserId
    ) {
        return@Authenticator null
    }

    return@Authenticator response.request.newBuilder()
        .header("Authorization", "Bearer $currentToken")
        .build()
}



                    // Perform synchronous refresh call directly via raw HTTP to prevent re-entrant interceptors
                    val refreshPayload = Gson().toJson(RefreshRequest(token = currentToken, refreshToken = currentRefreshToken))
                    val refreshRequest = Request.Builder()
                        .url(BuildConfig.BASE_URL.trimEnd('/') + "/api/v1/auth/refresh")
                        .post(refreshPayload.toRequestBody("application/json; charset=utf-8".toMediaType()))
                        .build()

                    try {
                        val baseClient = getDirectHttpClient()
                        val refreshResponse = baseClient.newCall(refreshRequest).execute()

                        if (refreshResponse.isSuccessful) {
                            val bodyString = refreshResponse.body?.string()
                            val refreshed = Gson().fromJson(bodyString, RefreshResponse::class.java)

                            if (refreshed != null && refreshed.token.isNotBlank()) {
                                TokenStore.saveTokens(
                                    ctx,
                                    refreshed.token,
                                    refreshed.refreshToken.ifBlank { currentRefreshToken }
                                )


                                return@Authenticator response.request.newBuilder()
                                    .header("Authorization", "Bearer ${refreshed.token}")
                                    .build()
                            }

                        }

                        return@Authenticator null
                    } catch (e: Exception) {
                        return@Authenticator null
                    }
                }
            }

            val builder = OkHttpClient.Builder()
                .addInterceptor(authInterceptor)
                .authenticator(tokenAuthenticator)
                .addInterceptor(logging)
                .followRedirects(true)
                .followSslRedirects(true)
                .connectTimeout(30, TimeUnit.SECONDS)
                .readTimeout(30, TimeUnit.SECONDS)
                .writeTimeout(30, TimeUnit.SECONDS)

            if (BuildConfig.DEBUG) {
                configureUnsafeSsl(builder)
            }


            val client = builder.build()

            val built = Retrofit.Builder()
                .baseUrl(BuildConfig.BASE_URL)   // must end with "/"
                .client(client)
                .addConverterFactory(GsonConverterFactory.create())
                .build()

            retrofit = built
            return built
        }
    }

    private fun responseCount(response: Response): Int {
        var result = 1
        var prior = response.priorResponse
        while (prior != null) {
            result++
            prior = prior.priorResponse
        }
        return result
    }

    private fun getDirectHttpClient(): OkHttpClient {
        val b = OkHttpClient.Builder()
            .connectTimeout(15, TimeUnit.SECONDS)
            .readTimeout(15, TimeUnit.SECONDS)
        if (BuildConfig.DEBUG) {
            configureUnsafeSsl(b)
        }
        return b.build()
    }

    private fun configureUnsafeSsl(builder: OkHttpClient.Builder) {
        try {
            val trustAllCerts = arrayOf<javax.net.ssl.TrustManager>(
                object : javax.net.ssl.X509TrustManager {
                    override fun checkClientTrusted(chain: Array<out java.security.cert.X509Certificate>?, authType: String?) {}
                    override fun checkServerTrusted(chain: Array<out java.security.cert.X509Certificate>?, authType: String?) {}
                    override fun getAcceptedIssuers(): Array<java.security.cert.X509Certificate> = emptyArray()
                }
            )
            val sslContext = javax.net.ssl.SSLContext.getInstance("SSL")
            sslContext.init(null, trustAllCerts, java.security.SecureRandom())
            val sslSocketFactory = sslContext.socketFactory
            builder.sslSocketFactory(sslSocketFactory, trustAllCerts[0] as javax.net.ssl.X509TrustManager)
            builder.hostnameVerifier { _, _ -> true }
        } catch (e: Exception) {
            android.util.Log.e("ApiClient", "Error configuring unsafe SSL", e)
        }
    }


    fun authService(context: Context): AuthService =
        retrofit(context).create(AuthService::class.java)

    fun siteReportService(context: Context): SiteReportService =
        retrofit(context).create(SiteReportService::class.java)

    fun officeService(context: Context): OfficeApiService =
        retrofit(context).create(OfficeApiService::class.java)

    fun attendanceService(context: Context): AttendanceService =
        retrofit(context).create(AttendanceService::class.java)
        
    fun aiService(context: Context): AiService =
        retrofit(context).create(AiService::class.java)

    fun expenseService(context: Context): ExpenseService =
        retrofit(context).create(ExpenseService::class.java)
        
    fun adminService(context: Context): AdminService =
        retrofit(context).create(AdminService::class.java)

    fun taskService(context: Context): TaskService =
        retrofit(context).create(TaskService::class.java)

    fun userService(context: Context): UserService =
        retrofit(context).create(UserService::class.java)

    fun locationService(context: Context): LocationService =
        retrofit(context).create(LocationService::class.java)

    fun materialsService(context: Context): MaterialsService =
        retrofit(context).create(MaterialsService::class.java)

    fun budgetService(context: Context): BudgetService =
        retrofit(context).create(BudgetService::class.java)
}
