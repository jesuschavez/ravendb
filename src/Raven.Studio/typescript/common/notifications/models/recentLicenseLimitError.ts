/// <reference path="../../../../typings/tsd.d.ts" />

import recentError = require("common/notifications/models/recentError");
import generalUtils = require("common/generalUtils");

class recentLicenseLimitError extends recentError {

    licenseLimitType = ko.observable<Raven.Server.Commercial.LimitType>();
    
    constructor(dto: recentErrorDto, limitType: Raven.Server.Commercial.LimitType) {
        super(dto);

        this.hasDetails = ko.pureComputed(() => true); // it always has details

        this.licenseLimitType(limitType);
    }

    static tryExtractLicenseLimitType(details: string): Raven.Server.Commercial.LimitType {
        try {
            const parsedDetails = JSON.parse(details);

            if (parsedDetails && parsedDetails.Type) {
                return parsedDetails.Type;
            }
        } catch (e) {
        }

        return null;
    }

}

export = recentLicenseLimitError;
