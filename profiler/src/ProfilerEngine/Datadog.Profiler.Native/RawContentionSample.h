// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "RawSample.h"
#include "Sample.h"

class RawContentionSample : public RawSample
{
public:
    inline static const std::string BucketLabelName = "Duration bucket";
    inline static const std::string RawCountLabelName = "raw count";
    inline static const std::string RawDurationLabelName = "raw duration";
    inline static const std::string BlockingThreadIdLabelName = "blocking thread id";
    inline static const std::string BlockingThreadNameLabelName = "blocking thread name";

public:
    RawContentionSample() = default;

    RawContentionSample(RawContentionSample&& other) noexcept
        :
        RawSample(std::move(other)),
        ContentionDuration(other.ContentionDuration),
        Bucket(std::move(other.Bucket)),
        BlockingThreadId(other.BlockingThreadId),
        BlockingThreadName(std::move(other.BlockingThreadName))
    {
    }

    RawContentionSample& operator=(RawContentionSample&& other) noexcept
    {
        if (this != &other)
        {
            RawSample::operator=(std::move(other));
            ContentionDuration = other.ContentionDuration;
            Bucket = std::move(other.Bucket);
            BlockingThreadId = other.BlockingThreadId;
            BlockingThreadName = std::move(other.BlockingThreadName);
        }
        return *this;
    }

    void OnTransform(std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& valueOffsets) const override
    {
        assert(valueOffsets.size() == 2);
        auto contentionCountIndex = valueOffsets[0];
        auto contentionDurationIndex = valueOffsets[1];

        sample->AddLabel(StringLabel{BucketLabelName, std::move(Bucket)});
        sample->AddValue(1, contentionCountIndex);
        sample->AddLabel(NumericLabel{RawCountLabelName, 1});
        sample->AddLabel(NumericLabel{RawDurationLabelName, ContentionDuration.count()});
        sample->AddValue(ContentionDuration.count(), contentionDurationIndex);
        if (BlockingThreadId != 0)
        {
            sample->AddLabel(NumericLabel{BlockingThreadIdLabelName, BlockingThreadId});
            sample->AddLabel(StringLabel{BlockingThreadNameLabelName, shared::ToString(BlockingThreadName)});
        }
    }

    std::chrono::nanoseconds ContentionDuration;
    std::string Bucket;
    uint64_t BlockingThreadId;
    shared::WSTRING BlockingThreadName;
};