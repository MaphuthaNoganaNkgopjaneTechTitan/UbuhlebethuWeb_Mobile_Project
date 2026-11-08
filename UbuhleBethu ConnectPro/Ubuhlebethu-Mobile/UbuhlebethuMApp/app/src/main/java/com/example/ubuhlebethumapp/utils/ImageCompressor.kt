package com.example.ubuhlebethumapp.utils

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Matrix
import android.media.ExifInterface
import android.util.Log
import java.io.File
import java.io.FileOutputStream
import java.io.IOException
import java.util.Locale

/**
 * Mobile Camera Auto-Compression utility for Fast Sync.
 * Automatically downsamples, rotates based on EXIF metadata, and compresses photos
 * into optimized JPEG format to avoid OutOfMemoryError and drastically reduce upload payloads.
 */
object ImageCompressor {

    private const val TAG = "ImageCompressor"

    const val DEFAULT_MAX_DIMENSION = 1920
    const val DEFAULT_QUALITY = 85

    data class CompressionResult(
        val originalSizeBytes: Long,
        val compressedSizeBytes: Long,
        val width: Int,
        val height: Int,
        val success: Boolean
    )

    /**
     * Compresses the specified image file in place.
     *
     * @param file Target image file to compress
     * @param maxDimension Target maximum width or height in pixels (default 1920)
     * @param quality JPEG compression quality between 10 and 100 (default 85)
     * @return CompressionResult with before and after sizes, dimensions, and success flag
     */
    fun compressImageFile(
        file: File,
        maxDimension: Int = DEFAULT_MAX_DIMENSION,
        quality: Int = DEFAULT_QUALITY
    ): CompressionResult {
        if (!file.exists() || !file.isFile) {
            Log.w(TAG, "File does not exist or is not a file: ${file.absolutePath}")
            return CompressionResult(0L, 0L, 0, 0, false)
        }

        val originalSizeBytes = file.length()
        if (originalSizeBytes <= 0L) {
            Log.w(TAG, "File is empty: ${file.absolutePath}")
            return CompressionResult(0L, 0L, 0, 0, false)
        }

        return try {
            // 1. Read EXIF orientation to avoid rotated photos
            val orientation = readExifOrientation(file.absolutePath)

            // 2. Decode bounds only (zero memory allocation)
            val boundsOptions = BitmapFactory.Options().apply {
                inJustDecodeBounds = true
            }
            BitmapFactory.decodeFile(file.absolutePath, boundsOptions)

            val rawWidth = boundsOptions.outWidth
            val rawHeight = boundsOptions.outHeight

            if (rawWidth <= 0 || rawHeight <= 0) {
                Log.w(TAG, "Could not decode image bounds for ${file.name}")
                return CompressionResult(originalSizeBytes, originalSizeBytes, 0, 0, false)
            }

            // Quick check: If the image is already lightweight, upright, and within bounds, skip recompression
            val isNormalOrientation = orientation == ExifInterface.ORIENTATION_NORMAL ||
                    orientation == ExifInterface.ORIENTATION_UNDEFINED
            val maxRawEdge = maxOf(rawWidth, rawHeight)
            if (isNormalOrientation && maxRawEdge <= maxDimension && originalSizeBytes <= 700 * 1024) {
                Log.d(
                    TAG,
                    "Image already optimized: ${file.name} (${formatFileSize(originalSizeBytes)}, ${rawWidth}x${rawHeight})"
                )
                return CompressionResult(
                    originalSizeBytes = originalSizeBytes,
                    compressedSizeBytes = originalSizeBytes,
                    width = rawWidth,
                    height = rawHeight,
                    success = true
                )
            }

            // 3. Subsample to prevent OutOfMemoryError on high-megapixel sensors
            var inSampleSize = calculateInSampleSize(rawWidth, rawHeight, maxDimension)
            var decodedBitmap: Bitmap? = null

            // Try decoding with calculated sample size; handle OOM gracefully by doubling sample size
            for (attempt in 0..1) {
                try {
                    val decodeOptions = BitmapFactory.Options().apply {
                        this.inSampleSize = inSampleSize
                        inPreferredConfig = Bitmap.Config.ARGB_8888
                    }
                    decodedBitmap = BitmapFactory.decodeFile(file.absolutePath, decodeOptions)
                    if (decodedBitmap != null) break
                } catch (_: OutOfMemoryError) {
                    Log.w(TAG, "OOM decoding ${file.name} with sampleSize=$inSampleSize, retrying with doubled sampleSize")
                    inSampleSize *= 2
                    System.gc()
                }
            }

            if (decodedBitmap == null) {
                Log.w(TAG, "Failed to decode bitmap from ${file.name}")
                return CompressionResult(originalSizeBytes, originalSizeBytes, rawWidth, rawHeight, false)
            }

            // 4. Matrix transformation for rotation and exact scaling down to maxDimension
            val matrix = Matrix()
            applyExifOrientation(matrix, orientation)

            val decodedMaxEdge = maxOf(decodedBitmap.width, decodedBitmap.height)
            if (decodedMaxEdge > maxDimension) {
                val scale = maxDimension.toFloat() / decodedMaxEdge.toFloat()
                matrix.postScale(scale, scale)
            }

            val finalBitmap = if (!matrix.isIdentity) {
                val transformed = Bitmap.createBitmap(
                    decodedBitmap,
                    0,
                    0,
                    decodedBitmap.width,
                    decodedBitmap.height,
                    matrix,
                    true
                )
                if (transformed != decodedBitmap) {
                    decodedBitmap.recycle()
                }
                transformed
            } else {
                decodedBitmap
            }

            val finalWidth = finalBitmap.width
            val finalHeight = finalBitmap.height

            // 5. Compress to temporary file
            val tempFile = File(file.parentFile, "compressed_${System.currentTimeMillis()}_${file.name}")
            var outStream: FileOutputStream? = null
            var compressionSuccess = false

            try {
                outStream = FileOutputStream(tempFile)
                val clampedQuality = quality.coerceIn(10, 100)
                compressionSuccess = finalBitmap.compress(Bitmap.CompressFormat.JPEG, clampedQuality, outStream)
                outStream.flush()
            } finally {
                try {
                    outStream?.close()
                } catch (_: IOException) {}
                finalBitmap.recycle()
            }

            if (!compressionSuccess || !tempFile.exists() || tempFile.length() <= 0L) {
                tempFile.delete()
                Log.w(TAG, "JPEG compression failed for ${file.name}")
                return CompressionResult(originalSizeBytes, originalSizeBytes, finalWidth, finalHeight, false)
            }

            val compressedSizeBytes = tempFile.length()

            // 6. Overwrite original file
            val replaced = try {
                tempFile.copyTo(file, overwrite = true)
                tempFile.delete()
                true
            } catch (e: Exception) {
                Log.w(TAG, "copyTo failed (${e.message}), attempting delete and rename")
                if (file.delete()) {
                    tempFile.renameTo(file)
                } else {
                    false
                }
            }

            if (!replaced || !file.exists()) {
                tempFile.delete()
                Log.w(TAG, "Failed to overwrite original file: ${file.name}")
                return CompressionResult(originalSizeBytes, originalSizeBytes, finalWidth, finalHeight, false)
            }

            // 7. Reset EXIF orientation on output file to NORMAL
            try {
                val newExif = ExifInterface(file.absolutePath)
                newExif.setAttribute(
                    ExifInterface.TAG_ORIENTATION,
                    ExifInterface.ORIENTATION_NORMAL.toString()
                )
                newExif.saveAttributes()
            } catch (e: Exception) {
                Log.w(TAG, "Could not normalize EXIF orientation: ${e.message}")
            }

            val reductionPct = if (originalSizeBytes > 0) {
                val saved = originalSizeBytes - compressedSizeBytes
                val pct = (saved.toDouble() / originalSizeBytes.toDouble()) * 100.0
                String.format(Locale.US, "%.1f%%", pct)
            } else {
                "0%"
            }

            Log.i(
                TAG,
                "Photo auto-compressed: ${file.name} [${formatFileSize(originalSizeBytes)} -> ${formatFileSize(compressedSizeBytes)}] (saved $reductionPct, ${finalWidth}x${finalHeight})"
            )

            CompressionResult(
                originalSizeBytes = originalSizeBytes,
                compressedSizeBytes = compressedSizeBytes,
                width = finalWidth,
                height = finalHeight,
                success = true
            )
        } catch (e: Exception) {
            Log.e(TAG, "Error compressing image file ${file.name}", e)
            CompressionResult(originalSizeBytes, originalSizeBytes, 0, 0, false)
        }
    }

    /**
     * Overload for file path string.
     */
    fun compressImageFile(
        filePath: String,
        maxDimension: Int = DEFAULT_MAX_DIMENSION,
        quality: Int = DEFAULT_QUALITY
    ): CompressionResult {
        return compressImageFile(File(filePath), maxDimension, quality)
    }

    private fun readExifOrientation(path: String): Int {
        return try {
            val exif = ExifInterface(path)
            exif.getAttributeInt(ExifInterface.TAG_ORIENTATION, ExifInterface.ORIENTATION_NORMAL)
        } catch (e: Exception) {
            Log.w(TAG, "Error reading EXIF for $path: ${e.message}")
            ExifInterface.ORIENTATION_NORMAL
        }
    }

    private fun applyExifOrientation(matrix: Matrix, orientation: Int) {
        when (orientation) {
            ExifInterface.ORIENTATION_ROTATE_90 -> matrix.postRotate(90f)
            ExifInterface.ORIENTATION_ROTATE_180 -> matrix.postRotate(180f)
            ExifInterface.ORIENTATION_ROTATE_270 -> matrix.postRotate(270f)
            ExifInterface.ORIENTATION_FLIP_HORIZONTAL -> matrix.postScale(-1f, 1f)
            ExifInterface.ORIENTATION_FLIP_VERTICAL -> matrix.postScale(1f, -1f)
            ExifInterface.ORIENTATION_TRANSPOSE -> {
                matrix.postRotate(90f)
                matrix.postScale(-1f, 1f)
            }
            ExifInterface.ORIENTATION_TRANSVERSE -> {
                matrix.postRotate(270f)
                matrix.postScale(-1f, 1f)
            }
            else -> {}
        }
    }

    fun calculateInSampleSize(width: Int, height: Int, maxDim: Int): Int {
        var inSampleSize = 1
        val largest = maxOf(width, height)
        var current = largest
        while (current / 2 >= maxDim) {
            inSampleSize *= 2
            current /= 2
        }
        return inSampleSize
    }

    fun formatFileSize(bytes: Long): String {
        if (bytes <= 0) return "0 B"
        val units = arrayOf("B", "KB", "MB", "GB")
        val digitGroups = (Math.log10(bytes.toDouble()) / Math.log10(1024.0)).toInt()
        val clampedGroup = digitGroups.coerceIn(0, units.size - 1)
        return String.format(
            Locale.US,
            "%.1f %s",
            bytes / Math.pow(1024.0, clampedGroup.toDouble()),
            units[clampedGroup]
        )
    }
}
